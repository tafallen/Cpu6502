namespace Cpu6502.Core;

/// <summary>
/// The memory/hardware bus the CPU talks to. Implementations handle ROM, RAM, and MMIO.
/// </summary>
public interface IBus
{
    byte Read(ushort address);
    void Write(ushort address, byte value);

    /// <summary>
    /// Attempts to retrieve a contiguous ReadOnlySpan&lt;byte&gt; for zero-copy memory reads.
    /// Returns true if the range is backed by a contiguous memory buffer, or false for custom MMIO devices.
    /// </summary>
    bool TryGetSpan(ushort address, int length, out ReadOnlySpan<byte> span)
    {
        span = default;
        return false;
    }
}
