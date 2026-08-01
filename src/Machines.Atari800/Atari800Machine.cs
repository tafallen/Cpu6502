using Cpu6502.Core;
using Machines.Common;

namespace Machines.Atari800;

/// <summary>
/// Machine container for Atari 800XL microcomputer target (1983).
/// Features SALLY 6502C CPU, 64 KB RAM, ANTIC video DMA, GTIA graphics, POKEY audio, and PIA 6520.
/// </summary>
public sealed class Atari800Machine
{
    public Cpu Cpu { get; }
    public AtariBus Bus { get; }

    private readonly Gtia _gtia;
    private readonly Pokey _pokey;
    private readonly Pia6520 _pia;
    private readonly Antic _antic;

    public Gtia Gtia => _gtia;
    public Pokey Pokey => _pokey;
    public Pia6520 Pia => _pia;
    public Antic Antic => _antic;
    public AtariKeyboardAdapter Keyboard { get; } = new();

    public const int CyclesPerFrame = 35_712; // 1.79 MHz / 50 Hz PAL

    public Atari800Machine(byte[]? osRom = null, byte[]? basicRom = null)
    {
        Bus = new AtariBus();
        _gtia = Bus.Gtia;
        _pokey = Bus.Pokey;
        _pia = Bus.Pia;
        _antic = Bus.Antic;

        if (osRom is not null && osRom.Length >= 0x4000)
            Array.Copy(osRom, 0, Bus.OsRom, 0, 0x4000);

        if (basicRom is not null && basicRom.Length >= 0x2000)
            Array.Copy(basicRom, 0, Bus.BasicRom, 0, 0x2000);

        _pokey.ReadKeyboard = () => Keyboard.ReadKeyCode();

        Cpu = new Cpu(Bus);
    }

    public void Reset() => Cpu.Reset();

    public void Step()
    {
        ulong cyclesBefore = Cpu.TotalCycles;
        Cpu.Step();
        int delta = (int)(Cpu.TotalCycles - cyclesBefore);

        _antic.Tick(delta);
        _pokey.Tick(delta);

        if (_pokey.Irq)
        {
            Cpu.Irq();
        }

        if (_antic.Nmi)
        {
            Cpu.Nmi();
        }
    }

    public void RunFrame(IVideoSink? sink = null)
    {
        ulong target = Cpu.TotalCycles + CyclesPerFrame;
        while (Cpu.TotalCycles < target)
        {
            Step();
        }

        if (sink is not null)
        {
            _antic.RenderFrame(Bus.Ram, _gtia, sink);
        }
    }
}
