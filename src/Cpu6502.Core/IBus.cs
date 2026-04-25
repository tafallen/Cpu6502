namespace Cpu6502.Core;

/// <summary>
/// The memory/hardware bus the CPU talks to. Implementations handle ROM, RAM, and MMIO.
/// </summary>
public interface IBus
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
}
