using Cpu6502.Core;
using Machines.Common;

namespace Machines.Atom;

/// <summary>
/// Acorn Atom machine compositor — accurate address map.
///
/// Address map:
///   $0000–$7FFF  Main RAM (up to 32KB; real hardware had 2KB standard, 12KB max on-board)
///   $8000–$9FFF  Video RAM (8KB, dual-ported: CPU and VDG both access it)
///   $A000–$AFFF  Optional ROM socket (#A: assembler / extended BASIC), 4KB
///   $B000–$B003  8255 PPI (keyboard, cassette, VDG mode pins)
///   $C000–$CFFF  Floating-point / arithmetic ROM, 4KB
///   $D000–$EFFF  BASIC ROM, 8KB
///   $F000–$FFFF  OS ROM (reset vector at $FFFC/$FFFD), 4KB
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
public sealed class AtomMachine
{
    public Cpu              Cpu      { get; }
    public Ram              MainRam  { get; }
    public Ram              VideoRam { get; }
    public Ppi8255          Ppi      { get; }
    public AtomTapeAdapter? Tape     { get; }

    private readonly Mc6847            _vdg;
    private readonly AddressDecoder    _bus;
    private readonly AtomSoundAdapter? _sound;

    private byte _lastPortC;

    /// <param name="basicRom">8KB BASIC ROM image, mapped at $D000–$EFFF.</param>
    /// <param name="osRom">4KB OS ROM image, mapped at $F000–$FFFF. Reset vector at offset $FFC/$FFD.</param>
    /// <param name="keyboard">Physical keyboard source. Pass null for headless/test use.</param>
    /// <param name="audio">Audio output sink. Pass null for silent/test use.</param>
    /// <param name="floatRom">Optional 4KB floating-point ROM, mapped at $C000–$CFFF.</param>
    /// <param name="extRom">Optional 4KB extension ROM (#A socket), mapped at $A000–$AFFF.</param>
    /// <param name="charRom">Optional 768-byte MC6847 character ROM (64 chars × 12 rows).</param>
    /// <param name="tape">Optional tape adapter. PC5 drives motor on/off; PC7 provides read data.</param>
    public AtomMachine(
        byte[] basicRom,
        byte[] osRom,
        IPhysicalKeyboard? keyboard = null,
        IAudioSink?        audio    = null,
        byte[]?            floatRom = null,
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
        if (extRom is not null)
            _bus.Map(0xA000, 0xAFFF, new Rom(extRom));
        _bus.Map(0xB000, 0xB003, Ppi);
        if (floatRom is not null)
            _bus.Map(0xC000, 0xCFFF, new Rom(floatRom));
        _bus.Map(0xD000, 0xEFFF, new Rom(basicRom));
        _bus.Map(0xF000, 0xFFFF, new Rom(osRom));

        Cpu = new Cpu(_bus);

        if (tape is not null)
        {
            // PC7 (bit 7) = cassette data in (active low: 0 = tone/1 bit, 0x80 = silence/0 bit)
            Ppi.ReadPortC = () =>
            {
                bool bit = tape.ReadBit(Cpu.TotalCycles);
                return (byte)(bit ? 0x00 : 0x80);
            };
        }
    }

    public void Reset() => Cpu.Reset();

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
            if (motorOn && !motorWasOn)  Tape.MotorOn(Cpu.TotalCycles);
            if (!motorOn && motorWasOn)  Tape.MotorOff(Cpu.TotalCycles);
        }

        _sound?.NotifyPortC(portC, Cpu.TotalCycles);
        _lastPortC = portC;
    }

    /// <summary>
    /// Run one video frame worth of CPU cycles (20 000 at 1 MHz / 50 Hz),
    /// notifying the sound adapter throughout so it can synthesise audio.
    /// </summary>
    public void RunFrame()
    {
        _sound?.BeginFrame(Cpu.TotalCycles);
        ulong target = Cpu.TotalCycles + AtomSoundAdapter.CyclesPerFrame;
        while (Cpu.TotalCycles < target)
            Step();
        _sound?.EndFrame(Cpu.TotalCycles);
    }

    /// <summary>Render the current video frame to the given sink.</summary>
    public void RenderFrame(IVideoSink sink)
    {
        _vdg.Control = Ppi.PortCLatch;
        _vdg.RenderFrame(sink);
    }
}
