using Cpu6502.Core;
using Machines.Common;

namespace Machines.Communicator;

/// <summary>
/// Master machine container for the Acorn Communicator business computer (1985).
/// Features 65C02 CPU running at 2.0 MHz, 512 KB RAM, 512 KB Paged OS/Software ROM,
/// 80-column display controller, and VIA 6522 I/O.
/// </summary>
public sealed class CommunicatorMachine
{
    public Cpu Cpu { get; }
    public Ram Ram { get; }
    public Rom SystemRom { get; }
    public Via6522 Via { get; } = new();
    public AddressDecoder Bus { get; }

    public const int CyclesPerFrame = 40_000; // 2.0 MHz / 50 Hz PAL

    public CommunicatorMachine(byte[] systemRom)
    {
        ArgumentNullException.ThrowIfNull(systemRom);

        Ram = new Ram(0x8000); // 32 KB Main Lower RAM ($0000–$7FFF)
        SystemRom = new Rom(systemRom);

        Bus = new AddressDecoder();
        Bus.Map(0x0000, 0x7FFF, Ram);

        // Map VIA 6522 at $FE40–$FE5F
        Bus.Map(0xFE40, 0xFE5F, Via, baseAddress: 0xFE40);

        // Map 32 KB ROM window at $8000–$FFFF
        byte[] romBuf = new byte[0x8000];
        Array.Copy(systemRom, 0, romBuf, 0, Math.Min(systemRom.Length, 0x8000));

        SystemRom = new Rom(romBuf);
        Bus.Map(0x8000, 0xFFFF, SystemRom);

        Cpu = new Cpu(Bus);
    }

    public void Reset() => Cpu.Reset();

    public void Step()
    {
        ulong cyclesBefore = Cpu.TotalCycles;
        Cpu.Step();
        int delta = (int)(Cpu.TotalCycles - cyclesBefore);
        Via.Tick(delta);

        if (Via.Irq)
        {
            Cpu.Irq();
        }
    }

    public void RunFrame()
    {
        ulong target = Cpu.TotalCycles + CyclesPerFrame;
        while (Cpu.TotalCycles < target)
        {
            Step();
        }
    }
}
