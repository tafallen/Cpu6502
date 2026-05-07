namespace Cpu6502.Core;

/// <summary>
/// Addressing mode page-cross cycle penalty consolidation.
/// Provides unified methods for applying conditional and unconditional cycle penalties
/// when address offsets cross page boundaries.
/// </summary>
public sealed partial class Cpu
{
    /// <summary>
    /// Apply conditional page-cross penalty: +1 cycle if pages differ (or alwaysAddCycle=true).
    /// Used by indexed addressing modes: Absolute,X / Absolute,Y / (Indirect),Y for read operations.
    /// </summary>
    /// <param name="baseAddr">Original address before offset added</param>
    /// <param name="effectiveAddr">Final address after offset added</param>
    /// <param name="alwaysAddCycle">If true, always add +1 cycle (e.g., for write operations on Absolute,X)</param>
    private void ApplyPageCrossPenalty(ushort baseAddr, ushort effectiveAddr, bool alwaysAddCycle = false)
    {
        if (alwaysAddCycle || PageCrossed(baseAddr, effectiveAddr))
            TotalCycles++;
    }

    /// <summary>
    /// Check if two addresses are on different pages (differ in high byte).
    /// Page boundary is at 0x0100, 0x0200, etc. (high byte changes).
    /// </summary>
    private static bool PageCrossed(ushort a, ushort b) => (a & CpuConstants.PAGE_MASK) != (b & CpuConstants.PAGE_MASK);

    /// <summary>
    /// Apply page-cross penalty for branch instructions.
    /// Branches take +1 cycle if page crossed. Formula: PC += signed_offset.
    /// </summary>
    private void ApplyBranchPageCrossPenalty(ushort currentPc, ushort targetPc)
    {
        if (PageCrossed(currentPc, targetPc))
            TotalCycles++;
    }
}
