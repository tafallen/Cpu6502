using Cpu6502.Core;

namespace Machines.C64;

/// <summary>
/// Machine container for Commodore 64 microcomputer target (1982).
/// Features MOS 6510 CPU, 64 KB RAM, KERNAL/BASIC/Char ROM banking.
/// </summary>
public sealed class C64Machine
{
    public Cpu Cpu { get; }
    public C64Bus Bus { get; }

    public const int CyclesPerFrame = 20_000; // 1.023 MHz / 50 Hz PAL

    public C64Machine(byte[]? kernalRom = null, byte[]? basicRom = null, byte[]? charRom = null)
    {
        Bus = new C64Bus();

        if (kernalRom is not null && kernalRom.Length >= 0x2000)
            Array.Copy(kernalRom, 0, Bus.KernalRom, 0, 0x2000);

        if (basicRom is not null && basicRom.Length >= 0x2000)
            Array.Copy(basicRom, 0, Bus.BasicRom, 0, 0x2000);

        if (charRom is not null && charRom.Length >= 0x1000)
            Array.Copy(charRom, 0, Bus.CharRom, 0, 0x1000);

        Cpu = new Cpu(Bus);
    }

    public void Reset() => Cpu.Reset();

    public void Step() => Cpu.Step();

    public void RunFrame()
    {
        ulong target = Cpu.TotalCycles + CyclesPerFrame;
        while (Cpu.TotalCycles < target)
        {
            Step();
        }
    }
}
