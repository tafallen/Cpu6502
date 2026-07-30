namespace Cpu6502.Core;

/// <summary>
/// Optional interface for memory devices (e.g. RAM, ROM) that provide direct byte array backing.
/// Allows AddressDecoder to bypass interface dispatch for fast flat memory reads and writes.
/// </summary>
public interface IDirectMemoryDevice : IBus
{
    /// <summary>Direct array buffer for fast reads, or null if custom read logic is required.</summary>
    byte[]? DirectReadBuffer { get; }

    /// <summary>Direct array buffer for fast writes, or null if custom write logic or ROM behavior is required.</summary>
    byte[]? DirectWriteBuffer { get; }
}
