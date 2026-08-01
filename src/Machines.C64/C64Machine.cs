using Cpu6502.Core;
using Machines.Common;

namespace Machines.C64;

/// <summary>
/// Machine container for Commodore 64 microcomputer target (1982).
/// Features MOS 6510 CPU, 64 KB RAM, VIC-II video generator, dual CIA controllers, and keyboard matrix.
/// </summary>
public sealed class C64Machine
{
    public Cpu Cpu { get; }
    public C64Bus Bus { get; }

    private readonly Vic2Video _vic;
    private readonly Sid6581 _sid;
    private readonly Cia6526 _cia1;
    private readonly Cia6526 _cia2;

    public Vic2Video Vic => _vic;
    public Sid6581 Sid => _sid;
    public Cia6526 Cia1 => _cia1;
    public Cia6526 Cia2 => _cia2;
    public C64KeyboardAdapter Keyboard { get; } = new();

    public const int CyclesPerFrame = 20_000; // 1.023 MHz / 50 Hz PAL

    public C64Machine(byte[]? kernalRom = null, byte[]? basicRom = null, byte[]? charRom = null)
    {
        Bus = new C64Bus();
        _vic = Bus.Vic;
        _sid = Bus.Sid;
        _cia1 = Bus.Cia1;
        _cia2 = Bus.Cia2;

        if (kernalRom is not null && kernalRom.Length >= 0x2000)
            Array.Copy(kernalRom, 0, Bus.KernalRom, 0, 0x2000);

        if (basicRom is not null && basicRom.Length >= 0x2000)
            Array.Copy(basicRom, 0, Bus.BasicRom, 0, 0x2000);

        if (charRom is not null && charRom.Length >= 0x1000)
            Array.Copy(charRom, 0, Bus.CharRom, 0, 0x1000);

        _cia1.ReadPortB = () => Keyboard.ReadRowState(_cia1.Pra);

        Cpu = new Cpu(Bus);
    }

    public void Reset() => Cpu.Reset();

    public void Step()
    {
        ulong cyclesBefore = Cpu.TotalCycles;
        Cpu.Step();
        int delta = (int)(Cpu.TotalCycles - cyclesBefore);

        _vic.Tick(delta);
        _cia1.Tick(delta);
        _cia2.Tick(delta);

        if (_cia1.Irq || _vic.Irq)
        {
            Cpu.Irq();
        }

        if (_cia2.Irq)
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
            _vic.RenderFrame(Bus.Ram, Bus.CharRom, sink);
        }
    }
}
