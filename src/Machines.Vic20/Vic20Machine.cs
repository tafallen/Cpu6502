using Cpu6502.Core;
using Machines.Common;

namespace Machines.Vic20;

/// <summary>
/// VIC-20 (unexpanded PAL) machine compositor.
///
/// Address map:
///   $0000–$00FF  Zero page (RAM)
///   $0100–$03FF  Stack + system area (RAM)
///   $0400–$0FFF  Unmapped (open bus / $FF)
///   $1000–$1FFF  Main RAM (4KB, unexpanded)
///   $2000–$7FFF  Expansion RAM area (unmapped → $FF)
///   $8000–$8FFF  Colour RAM (4-bit nibbles, 1KB usable; upper nibble open)
///   $9000–$900F  VIC-I video/audio registers
///   $9110–$911F  VIA 1 (serial bus, tape, joystick)
///   $9120–$912F  VIA 2 (keyboard matrix, joystick)
///   $A000–$BFFF  BASIC ROM (8KB)
///   $C000–$DFFF  Unmapped
///   $E000–$FFFF  Kernal ROM (8KB; reset vector at $FFFC/$FFFD)
///
/// Emulator loop:
///   machine.Reset();
///   while (running)
///   {
///       host.PollEvents();
///       machine.RunFrame();
///       machine.RenderFrame(host);
///   }
/// </summary>
public sealed class Vic20Machine
{
    // ── public hardware ──────────────────────────────────────────────────────

    public Cpu              Cpu      { get; }
    public Ram              Ram      { get; }     // $0000–$03FF + $1000–$1FFF
    public Via6522          Via1     { get; }     // $9110–$911F
    public Via6522          Via2     { get; }     // $9120–$912F
    public IBus             Bus      { get; }     // full address decoder
    public Vic20TapeAdapter? Tape    { get; }

    private readonly VicI      _vic;
    private readonly Ram       _colorRam;          // 1KB at $8000
    private readonly AddressDecoder _bus;

    // Frames: PAL VIC-20 runs at 1,108,405 Hz / 50 Hz = 22,168 cycles/frame
    private const int CyclesPerFrame = 22_168;

    /// <param name="basicRom">8KB BASIC ROM image (mapped at $A000–$BFFF).</param>
    /// <param name="kernalRom">8KB Kernal ROM image (mapped at $E000–$FFFF).</param>
    /// <param name="charRom">Optional 4KB character ROM (VIC-I address space $8000–$8FFF).</param>
    /// <param name="keyboard">Physical keyboard source. Pass null for headless/test use.</param>
    /// <param name="audio">Audio sink. Pass null for silent/test use.</param>
    /// <param name="tape">Optional tape adapter. Motor driven by VIA 1 Port B bit 3.</param>
    public Vic20Machine(
        byte[]              basicRom,
        byte[]              kernalRom,
        byte[]?             charRom  = null,
        IPhysicalKeyboard?  keyboard = null,
        IAudioSink?         audio    = null,
        Vic20TapeAdapter?   tape     = null)
    {
        Ram       = new Ram(0x2000);   // covers $0000–$1FFF (4KB zero+stack+main)
        _colorRam = new Ram(0x0400);   // 1KB at $8000
        Via1      = new Via6522();
        Via2      = new Via6522();
        _vic      = new VicI(audio);
        Tape      = tape;

        // Wire keyboard to VIA 2
        if (keyboard is not null)
        {
            var kbAdapter = new Vic20KeyboardAdapter(keyboard);
            Via2.ReadPortA = () => kbAdapter.ScanColumns(Via2.PortBLatch);
        }

        // Wire tape: VIA 1 CB1 receives tape signal edges
        if (tape is not null)
        {
            tape.OnEdge = level => Via1.SetCB1(level);
        }

        // VIC-I memory callbacks
        _vic.ReadVicMemory = ReadVicMemory;
        _vic.ReadColorRam  = cellIdx => (byte)(_colorRam.Read((ushort)(cellIdx & 0x3FF)) & 0x0F);

        _bus = new AddressDecoder();
        _bus.Map(0x0000, 0x1FFF, Ram);
        _bus.Map(0x8000, 0x83FF, _colorRam);
        _bus.Map(0x9000, 0x900F, _vic);
        _bus.Map(0x9110, 0x911F, Via1);
        _bus.Map(0x9120, 0x912F, Via2);
        _bus.Map(0xA000, 0xBFFF, new Rom(basicRom));
        _bus.Map(0xE000, 0xFFFF, new Rom(kernalRom));

        Bus = _bus;
        Cpu = new Cpu(_bus);
    }

    public void Reset() => Cpu.Reset();

    public void Step()
    {
        ulong before = Cpu.TotalCycles;
        Cpu.Step();
        int cycles = (int)(Cpu.TotalCycles - before);

        Via1.Tick(cycles);
        Via2.Tick(cycles);

        if (Via1.Irq || Via2.Irq)
            Cpu.Irq();

        // Tape motor: VIA 1 Port B bit 3 (CB2 relay on real hardware)
        if (Tape is not null)
        {
            bool motorOn = (Via1.PortBLatch & 0x08) != 0;
            Tape.SetMotor(motorOn, Cpu.TotalCycles);
            Tape.Tick(Cpu.TotalCycles);
        }
    }

    public void RunFrame()
    {
        ulong target = Cpu.TotalCycles + CyclesPerFrame;
        while (Cpu.TotalCycles < target)
            Step();
    }

    public void RenderFrame(IVideoSink sink) => _vic.RenderFrame(sink);

    // ── VIC-I 14-bit address space translation ────────────────────────────────
    //
    // The unexpanded VIC-20 wires VIC address lines as follows:
    //   VIC $0000–$0FFF → CPU $8000–$8FFF (colour RAM / char ROM area)
    //   VIC $1000–$1FFF → CPU $1000–$1FFF (screen RAM + main RAM)
    //   VIC $2000–$2FFF → CPU $0000–$0FFF (zero page etc.)
    //   VIC $3000–$3FFF → CPU $8000–$8FFF (mirrors char ROM)
    //
    // Standard unexpanded config: screen at $1E00 (VIC $1E00), chars at VIC $8000 (CPU $8000).

    private byte ReadVicMemory(ushort vicAddr)
    {
        ushort cpuAddr = vicAddr switch
        {
            >= 0x0000 and <= 0x0FFF => (ushort)(0x8000 | (vicAddr & 0x0FFF)),
            >= 0x1000 and <= 0x1FFF => vicAddr,
            >= 0x2000 and <= 0x2FFF => (ushort)(vicAddr & 0x0FFF),
            _                       => (ushort)(0x8000 | (vicAddr & 0x0FFF)),
        };
        return _bus.Read(cpuAddr);
    }
}
