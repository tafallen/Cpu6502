using Cpu6502.Core;
using Machines.Common;

namespace Machines.Electron;

/// <summary>
/// Machine container for Acorn Electron microcomputer target (1983).
/// Features 6502 CPU, 32 KB RAM ($0000–$7FFF), and Electron ULA controller ($8000–$FFFF).
/// </summary>
public sealed class ElectronMachine
{
    public Cpu Cpu { get; }
    public Ram Ram { get; }
    public ElectronUla Ula { get; }
    public AddressDecoder Bus { get; }

    public const int DefaultCyclesPerFrame = 40_000; // 2 MHz / 50 Hz PAL

    public ElectronMachine(byte[] osRom, byte[] basicRom)
    {
        Ram = new Ram(0x8000); // 32 KB RAM ($0000–$7FFF)
        Ula = new ElectronUla(basicRom: basicRom, osRom: osRom);

        Bus = new AddressDecoder();
        Bus.Map(0x0000, 0x7FFF, Ram);
        Bus.Map(0x8000, 0xFFFF, Ula, 0x8000);

        Cpu = new Cpu(Bus);
    }

    public void Reset() => Cpu.Reset();

    public void Step() => Cpu.Step();

    public void RunFrame(int cyclesPerFrame = DefaultCyclesPerFrame)
    {
        ulong target = Cpu.TotalCycles + (ulong)cyclesPerFrame;
        while (Cpu.TotalCycles < target)
        {
            Cpu.Step();
        }
    }
}
