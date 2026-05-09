namespace Cpu6502.Core;

/// <summary>
/// Zero-cost no-op trace implementation.
/// All methods are empty, allowing the compiler to inline and eliminate the calls entirely.
/// Used as the default Cpu.Trace to ensure tracing has zero performance impact when disabled.
/// </summary>
public sealed class NullTrace : IExecutionTrace
{
    /// <summary>Singleton instance (sealed class, can be eagerly instantiated).</summary>
    public static readonly NullTrace Instance = new();

    private NullTrace() { }

    public void OnInstructionFetched(ushort pc, byte opcode) { }
    public void OnInstructionExecuted(ushort pc, byte opcode, int cycles, byte aAfter, byte flags) { }
    public void OnMemoryAccess(ushort address, byte value, bool isWrite, ulong cycles) { }
    public void OnInterrupt(InterruptType type, ushort handlerAddress) { }
    public bool ShouldBreak(ushort pc, byte opcode, byte currentA) => false;
}
