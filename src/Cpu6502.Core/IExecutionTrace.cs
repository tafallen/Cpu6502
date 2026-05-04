namespace Cpu6502.Core;

/// <summary>
/// Execution trace event sink for debugging, profiling, and test instrumentation.
/// Allows orthogonal capture of instruction execution, memory access, and interrupt events.
/// Default implementation (NullTrace) is a zero-cost no-op; tracing is opt-in via Cpu.Trace property.
/// </summary>
public interface IExecutionTrace
{
    /// <summary>
    /// Called when an instruction opcode is fetched (before dispatch).
    /// Allows breakpoints or early-exit logic before instruction executes.
    /// </summary>
    void OnInstructionFetched(ushort pc, byte opcode);

    /// <summary>
    /// Called after an instruction completes execution.
    /// Provides full state snapshot: PC (before execution), opcode, total cycle cost, A register, processor status flags.
    /// </summary>
    void OnInstructionExecuted(ushort pc, byte opcode, int cycles, byte aAfter, byte flags);

    /// <summary>
    /// Called on every memory access (read or write).
    /// Allows memory breakpoints, access logging, page-table visualization.
    /// Called for all bus reads/writes: fetch, data read, data write, stack ops, etc.
    /// </summary>
    /// <param name="address">Memory address being accessed</param>
    /// <param name="value">Byte value being read or written</param>
    /// <param name="isWrite">True for write, false for read</param>
    /// <param name="cycles">CPU cycle count at time of access (for correlation)</param>
    void OnMemoryAccess(ushort address, byte value, bool isWrite, ulong cycles);

    /// <summary>
    /// Called when an interrupt (IRQ or NMI) is being serviced.
    /// Provides interrupt type and resolved handler address from interrupt vector.
    /// Allows interrupt breakpoints and interrupt vector verification.
    /// </summary>
    void OnInterrupt(InterruptType type, ushort handlerAddress);

    /// <summary>
    /// Optional filter to control which memory accesses are recorded.
    /// Return false to skip recording this access (useful for high-volume memory ranges).
    /// Default implementation (if not overridden) accepts all accesses.
    /// </summary>
    /// <param name="address">Memory address being accessed</param>
    /// <param name="isWrite">True for write, false for read</param>
    /// <returns>True to record this access, false to skip</returns>
    bool ShouldRecordMemoryAccess(ushort address, bool isWrite) => true;

    /// <summary>
    /// Optional sample rate for memory access recording (1 = record all, 2 = record every other, etc).
    /// Default is 1 (record all). Can be overridden for high-frequency tracing.
    /// </summary>
    int MemoryAccessSampleRate => 1;
}

/// <summary>
/// Interrupt type enumeration for trace events.
/// </summary>
public enum InterruptType
{
    Irq,  // Maskable interrupt (can be disabled via I flag)
    Nmi   // Non-maskable interrupt (always serviced)
}
