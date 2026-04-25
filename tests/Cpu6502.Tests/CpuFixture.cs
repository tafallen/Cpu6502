using Cpu6502.Core;

namespace Cpu6502.Tests;

/// <summary>
/// Base class for all CPU tests. Provides a 64KB Ram backed bus and helpers
/// for loading programs, stepping the CPU, and taking cycle snapshots.
/// </summary>
public abstract class CpuFixture
{
    protected readonly Ram   Ram = new(0x10000);
    protected readonly Cpu   Cpu;

    protected CpuFixture() => Cpu = new Cpu(Ram);

    /// <summary>Write bytes at <paramref name="address"/> and set PC there.</summary>
    protected void Load(ushort address, params byte[] bytes)
    {
        Ram.Load(address, bytes);
        SetPc(address);
    }

    protected void SetPc(ushort address)
    {
        // Write the address into the reset vector and call Reset to set PC
        Ram.Write(0xFFFC, (byte)(address & 0xFF));
        Ram.Write(0xFFFD, (byte)(address >> 8));
        Cpu.Reset();
    }

    protected void Step(int count = 1)
    {
        for (int i = 0; i < count; i++) Cpu.Step();
    }

    protected ulong CycleSnapshot() => Cpu.TotalCycles;
}
