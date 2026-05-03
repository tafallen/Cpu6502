using Cpu6502.Core;
using Machines.Common;

namespace Machines.Vic20;

/// <summary>
/// VIC-20 (unexpanded PAL) machine compositor.
///
/// Address map:
///   $0000–$00FF  Zero page (RAM)
///   $0100–$01FF  Stack (RAM)
///   $0200–$0FFF  System/work RAM
///   $1000–$1FFF  Main RAM (4KB, unexpanded)
///   $2000–$7FFF  Expansion RAM area (unmapped → $FF)
///   $8000–$8FFF  Colour RAM (4-bit nibbles, 1KB usable; upper nibble open)
///   $9000–$900F  VIC-I video/audio registers
///   $9110–$911F  VIA 1 (serial bus, tape, joystick)
///   $9120–$912F  VIA 2 (keyboard matrix, joystick)
///   $A000–$BFFF  Expansion cartridge area (Block 5; unmapped on unexpanded VIC-20)
///   $C000–$DFFF  BASIC ROM (8KB)
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
public sealed class Vic20Machine : IComponent
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
    private readonly byte[]    _charRom;           // 4KB, VIC-only — not on CPU bus
    private readonly AddressDecoder _bus;
    private readonly MachineClock _clock = new();
    private readonly TimingScheduler _scheduler;
    private ulong? _armedTapeEdgeCycle;
    private bool _irqWasActive;

    // Frames: PAL VIC-20 runs at 1,108,405 Hz / 50 Hz = 22,168 cycles/frame
    private const int CyclesPerFrame = 22_168;

    /// <param name="basicRom">8KB BASIC ROM image (mapped at $C000–$DFFF).</param>
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
        _charRom  = charRom ?? new byte[0x1000]; // 4KB; zeros if not supplied
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
        _bus.Map(0xC000, 0xDFFF, new Rom(basicRom));
        _bus.Map(0xE000, 0xFFFF, new Rom(kernalRom));

        Bus = _bus;
        Cpu = new Cpu(_bus);
        _scheduler = new TimingScheduler(_clock);
        Cpu.OnCyclesConsumed = AdvanceTiming;

        ValidateInitialization();
    }

    public void Reset()
    {
        Cpu.Reset();
        _clock.Set(Cpu.TotalCycles);
        _armedTapeEdgeCycle = null;
    }

    public void ValidateInitialization()
    {
        // Keyboard is optional for headless/test use, so no validation needed.
        // Future extensions can validate other component wiring as needed.
    }

    public void Step()
    {
        Cpu.Step();
    }

    private void AdvanceTiming(int cycles)
    {
        _clock.Advance(cycles);
        _scheduler.RunDue(_clock.Now);
        Via1.Tick(cycles);
        Via2.Tick(cycles);

        // Level-sensitive IRQ: only notify the CPU on the low-going edge (false→true).
        bool irqNow = Via1.Irq || Via2.Irq;
        if (irqNow && !_irqWasActive)
            Cpu.Irq();
        _irqWasActive = irqNow;

        // Tape motor: VIA 1 Port B bit 3 (CB2 relay on real hardware)
        if (Tape is not null)
        {
            bool motorOn = (Via1.PortBLatch & 0x08) != 0;
            ulong? before = Tape.GetNextEdgeCycle();
            Tape.SetMotor(motorOn, _clock.Now);

            if (!motorOn)
            {
                _armedTapeEdgeCycle = null;
            }
            else if (before is null)
            {
                ArmTapeEdge();
            }
        }
    }

    public void RunFrame()
    {
        ulong target = _clock.Now + CyclesPerFrame;
        while (_clock.Now < target)
            Step();
    }

    public void RenderFrame(IVideoSink sink) => _vic.RenderFrame(sink);

    // ── VIC-I 14-bit address space translation ────────────────────────────────
    //
    // The VIC chip's 14-bit address space maps to the VIC-20 system bus as follows:
    //   VIC $0000–$0FFF → Character ROM ($8000–$8FFF CPU; VIC-only, not on CPU bus)
    //   VIC $1000–$1FFF → CPU $9000–$9FFF (VIC/VIA registers; open bus for VIC reads)
    //   VIC $2000–$2FFF → CPU $0000–$0FFF (zero page / stack RAM)
    //   VIC $3000–$3FFF → CPU $1000–$1FFF (main user RAM)
    //
    // Default kernal config: screen base VIC $3E00 = CPU $1E00 (reg5=$F0, reg2 bit7=1),
    //                        char base  VIC $0000 = char ROM (reg5 low nibble = 0).

    private byte ReadVicMemory(ushort vicAddr)
    {
        if (vicAddr < 0x1000)
            // VIC $0000–$0FFF → character ROM
            return _charRom[vicAddr & 0x0FFF];

        if (vicAddr < 0x2000)
            // VIC $1000–$1FFF → CPU $9000–$9FFF (hardware registers, open bus for VIC)
            return 0xFF;

        // VIC $2000–$3FFF → CPU $0000–$1FFF
        return _bus.Read((ushort)(vicAddr - 0x2000));
    }

    private void ArmTapeEdge()
    {
        if (Tape is null) return;
        ulong? next = Tape.GetNextEdgeCycle();
        if (!next.HasValue) return;
        if (_armedTapeEdgeCycle == next.Value) return;

        _armedTapeEdgeCycle = next.Value;
        _scheduler.ScheduleAt(next.Value, ProcessTapeEdges);
    }

    private void ProcessTapeEdges()
    {
        if (Tape is null || !_armedTapeEdgeCycle.HasValue) return;
        if (_clock.Now < _armedTapeEdgeCycle.Value) return;

        _armedTapeEdgeCycle = null;
        Tape.Tick(_clock.Now); // catch up all due edges at/under current cycle
        ArmTapeEdge();
    }
}
