using Cpu6502.Core;
using Machines.Common;

namespace Machines.Atom;

/// <summary>
/// Acorn Atom machine compositor — accurate address map, implements IComponent for lifecycle validation.
///
/// Address map:
///   $0000–$7FFF  Main RAM (up to 32KB; real hardware had 2KB standard, 12KB max on-board)
///   $8000–$9FFF  Video RAM (8KB, dual-ported: CPU and VDG both access it)
///   $A000–$AFFF  Optional ROM socket (#A: assembler utility), 4KB  [axr1.rom]
///   $B000–$BFFF  8255 PPI (keyboard, cassette, VDG mode pins; partial decode mirrors)
///   $C000–$CFFF  BASIC ROM, 4KB                                     [abasic.rom]
///   $D000–$DFFF  Floating-point / arithmetic ROM, 4KB               [afloat.rom]
///   $E000–$EFFF  DOS / extension ROM, 4KB                           [dosrom.rom]
///   $F000–$FFFF  OS (kernel) ROM, 4KB                               [akernel.rom]
///
/// Emulator loop:
///   machine.Reset();
///   while (running)
///   {
///       host.PollEvents();
///       machine.RunFrame();          // steps CPU for one frame, generates audio
///       machine.RenderFrame(host);   // renders video
///   }
/// </summary>
public sealed class AtomMachine : IComponent
{
    public Cpu              Cpu      { get; }
    public IBus             Bus      { get; }
    public Ram              MainRam  { get; }
    public Ram              VideoRam { get; }
    public Ppi8255          Ppi      { get; }
    public AtomTapeAdapter? Tape     { get; }

    private readonly Mc6847            _vdg;
    private readonly AddressDecoder    _bus;
    private readonly AtomSoundAdapter? _sound;
    private readonly MachineClock      _clock = new();
    private readonly TimingScheduler   _scheduler;

    private byte _lastPortC;
    private bool _vbl;                  // true = vertical blank active (bit 7 of Port C = 0)
    private ulong _frameStartCycles;    // CPU cycle count at the start of the current frame

    // Approximate VBL duration: real MC6847 blanks ~4% of the frame.
    // At 20 000 cycles/frame (1 MHz, 50 Hz) that's about 800 cycles.
    private const ulong VblCycles = 800;

    /// <param name="basicRom">4KB BASIC ROM image (abasic.rom), mapped at $C000–$CFFF.</param>
    /// <param name="osRom">4KB OS/kernel ROM image (akernel.rom), mapped at $F000–$FFFF.</param>
    /// <param name="keyboard">Physical keyboard source. Pass null for headless/test use.</param>
    /// <param name="audio">Audio output sink. Pass null for silent/test use.</param>
    /// <param name="floatRom">4KB floating-point ROM (afloat.rom), mapped at $D000–$DFFF. RTS stubs used if null.</param>
    /// <param name="dosRom">4KB DOS/extension ROM (dosrom.rom), mapped at $E000–$EFFF. RTS stubs used if null.</param>
    /// <param name="extRom">4KB utility ROM (#A socket, axr1.rom), mapped at $A000–$AFFF.</param>
    /// <param name="charRom">Optional 768-byte MC6847 character ROM (64 chars × 12 rows).</param>
    /// <param name="tape">Optional tape adapter. PC5 drives motor on/off; PC7 provides read data.</param>
    public AtomMachine(
        byte[] basicRom,
        byte[] osRom,
        IPhysicalKeyboard? keyboard = null,
        IAudioSink?        audio    = null,
        byte[]?            floatRom = null,
        byte[]?            dosRom   = null,
        byte[]?            extRom   = null,
        byte[]?            charRom  = null,
        AtomTapeAdapter?   tape     = null)
    {
        MainRam  = new Ram(0x8000);
        VideoRam = new Ram(0x2000);

        Ppi = new Ppi8255();
        Ppi.Write(3, 0x8A); // PA=out, PB=in, PC-upper=in, PC-lower=out

        if (keyboard is not null)
        {
            var kb = new AtomKeyboardAdapter(keyboard);
            Ppi.ReadPortB = () => kb.ScanColumns(Ppi.PortALatch);
        }

        if (audio is not null)
            _sound = new AtomSoundAdapter(audio);

        Tape = tape;

        _vdg = new Mc6847(VideoRam.RawBytes, charRom);

        _bus = new AddressDecoder();
        _bus.Map(0x0000, 0x7FFF, MainRam);
        _bus.Map(0x8000, 0x9FFF, VideoRam);
        byte[] extImage;
        if (extRom is not null)
        {
            extImage = extRom;
        }
        else
        {
            // Without the assembler ROM, the OS IRQ handler JMPs through ($0204) → $A000.
            // The OS pushes A before that JMP, so the handler at $A000 must PLA then RTI.
            // All other entries stay as RTS ($60) stubs so JSR calls into this region return.
            extImage = new byte[0x1000];
            Array.Fill(extImage, (byte)0x60); // RTS stubs everywhere
            extImage[0x0000] = 0x68;          // $A000: PLA  (balance OS's PHA before JMP)
            extImage[0x0001] = 0x40;          // $A001: RTI  (restore P, PC from IRQ frame)
        }
        _bus.Map(0xA000, 0xAFFF, new Rom(extImage));
        _bus.Map(0xB000, 0xBFFF, Ppi); // partial decode: PPI mirrors across $B000-$BFFF

        // BASIC ROM at $C000-$CFFF (abasic.rom)
        _bus.Map(0xC000, 0xCFFF, new Rom(basicRom));

        // Floating-point ROM at $D000-$DFFF (afloat.rom); RTS stubs if absent
        byte[] floatImage = floatRom ?? MakeRtsStubs(0x1000);
        _bus.Map(0xD000, 0xDFFF, new Rom(floatImage));

        // DOS ROM at $E000-$EFFF (dosrom.rom); RTS stubs if absent
        byte[] dosImage = dosRom ?? MakeRtsStubs(0x1000);
        _bus.Map(0xE000, 0xEFFF, new Rom(dosImage));

        _bus.Map(0xF000, 0xFFFF, new Rom(osRom));

        Bus = _bus;
        Cpu = new Cpu(_bus);
        _scheduler = new TimingScheduler(_clock);
        Cpu.OnCyclesConsumed = OnCyclesConsumed;

        // Port C read bit layout (matches Atomulator / Acorn Atom hardware):
        //   bit 7: VBL — 0 during vertical blank, 1 during active display (active-low)
        //   bit 6: RPT key (unused here; stays 1)
        //   bit 5: tape data in (0 = tone present, 1 = silence)
        //   bit 4: intone (unused here; stays 1)
        //   bit 3: CSS (VDG colour select — driven by Port C out, reflected back)
        //   bit 2: speaker output (reflected from Port C out)
        Ppi.ReadPortC = () =>
        {
            byte val = 0xFF; // all high by default
            if (_vbl)        val &= 0x7F; // bit 7 = 0 during VBL
            if (tape is not null)
            {
                bool tone = tape.ReadBit(_clock.Now);
                if (tone) val &= 0xDF; // bit 5 = 0 when tone present
            }
            return val;
        };

        ValidateInitialization();
    }

    public void Reset()
    {
        Cpu.Reset();
        _clock.Set(Cpu.TotalCycles);
    }

    public void ValidateInitialization()
    {
        if (Cpu == null)
            throw new InvalidOperationException("CPU not initialized");
        
        if (MainRam == null || MainRam.RawBytes.Length == 0)
            throw new InvalidOperationException("Main RAM not initialized");
        
        if (VideoRam == null || VideoRam.RawBytes.Length == 0)
            throw new InvalidOperationException("Video RAM not initialized");
        
        if (Ppi == null)
            throw new InvalidOperationException("PPI8255 not initialized");
        
        // Keyboard is optional for headless/test use, so no validation needed.
        // Tape is optional for headless/test use, so no validation needed.
    }

    private static byte[] MakeRtsStubs(int size)
    {
        var img = new byte[size];
        Array.Fill(img, (byte)0x60); // RTS everywhere — JSR calls return safely
        return img;
    }

    /// <summary>Execute one CPU instruction and notify the sound and tape adapters of any PC change.</summary>
    public void Step()
    {
        Cpu.Step();

        byte portC = Ppi.PortCLatch;
        if (portC == _lastPortC) return;

        if (Tape is not null)
        {
            // PC5 = cassette motor relay
            bool motorOn    = (portC      & 0x20) != 0;
            bool motorWasOn = (_lastPortC & 0x20) != 0;
            if (motorOn && !motorWasOn)  Tape.MotorOn(_clock.Now);
            if (!motorOn && motorWasOn)  Tape.MotorOff(_clock.Now);
        }

        _sound?.NotifyPortC(portC, _clock.Now);
        _lastPortC = portC;
    }

    /// <summary>
    /// Run one video frame worth of CPU cycles (20 000 at 1 MHz / 50 Hz),
    /// notifying the sound adapter throughout so it can synthesise audio.
    /// </summary>
    public void RunFrame()
    {
        _frameStartCycles = _clock.Now;
        ulong target      = _frameStartCycles + AtomSoundAdapter.CyclesPerFrame;
        ulong vblEnd      = _frameStartCycles + VblCycles;

        // MC6847 /FS (field sync) fires at the start of vertical blank, wired to the 6502 IRQ.
        // VBL is active for the first ~800 cycles, matching real hardware timing so the OS
        // cursor-blink routine (which waits for VBL before toggling) fires promptly.
        _vbl = true;
        _scheduler.ScheduleAt(vblEnd, () => _vbl = false);
        Cpu.Irq();

        _sound?.BeginFrame(_clock.Now);
        while (_clock.Now < target)
            Step();
        _vbl = false;
        _sound?.EndFrame(_clock.Now);
    }

    /// <summary>Render the current video frame to the given sink.</summary>
    public void RenderFrame(IVideoSink sink)
    {
        // Atom PPI Port C wiring to MC6847: PC3=A/G, PC2=GM0, PC1=GM1, PC0=GM2, PC4=CSS
        // Mc6847.Control layout:             bit0=A/G, bit1=GM0, bit2=GM1, bit3=GM2, bit4=CSS
        byte portC = Ppi.PortCLatch;
        _vdg.Control = (byte)(
            ((portC >> 3) & 0x01) |  // PC3 → A/G  (bit 0)
            ((portC >> 1) & 0x02) |  // PC2 → GM0  (bit 1)
            ((portC << 1) & 0x04) |  // PC1 → GM1  (bit 2)
            ((portC << 3) & 0x08) |  // PC0 → GM2  (bit 3)
            ((portC << 0) & 0x10)    // PC4 → CSS  (bit 4, upper nibble input)
        );
        _vdg.RenderFrame(sink);
    }

    private void OnCyclesConsumed(int cycles)
    {
        _clock.Advance(cycles);
        _scheduler.RunDue(_clock.Now);
    }
}
