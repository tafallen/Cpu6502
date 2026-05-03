namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── Cycle metadata ───────────────────────────────────────────────────────────
    
    /// <summary>
    /// Addressing mode classification for cycle accounting.
    /// Used as key in CycleTable to determine base cycles and page-cross penalty applicability.
    /// </summary>
    public enum AddressingMode
    {
        Immediate,      // LDA #$42                 — PC+1
        ZeroPage,       // LDA $42                  — address at PC+1
        ZeroPageX,      // LDA $42,X                — address at PC+1, indexed with X
        ZeroPageY,      // LDA $42,Y                — address at PC+1, indexed with Y
        Absolute,       // LDA $1234                — address at PC+1:2
        AbsoluteX,      // LDA $1234,X              — address at PC+1:2, indexed with X (page-cross penalty possible)
        AbsoluteY,      // LDA $1234,Y              — address at PC+1:2, indexed with Y (page-cross penalty possible)
        IndirectX,      // LDA ($42,X)              — pointer at PC+1+X, dereferenced from zero page
        IndirectY,      // LDA ($42),Y              — pointer at PC+1, dereferenced from zero page, result indexed with Y (page-cross penalty possible)
        Indirect,       // JMP ($1234)              — address at PC+1:2 (subject to page-wrap bug)
        Relative,       // BEQ $42                  — signed offset at PC+1; page-cross penalty on branch taken
    }

    /// <summary>
    /// Memory access classification for cycle accounting.
    /// Different access types (read, write, read-modify-write) have different timing properties.
    /// </summary>
    public enum AccessType
    {
        Read,   // Load instructions and reads (e.g., LDA, CMP) — page-cross penalty may apply
        Write,  // Store instructions (e.g., STA) — no page-cross penalty (baked into base)
        Rmw,    // Read-Modify-Write (e.g., INC, ASL) — always includes page-cross overhead in base
    }

    /// <summary>
    /// Cycle timing properties for an (AddressingMode, AccessType) pair.
    /// BaseCycles includes the instruction fetch (first byte). Page-cross penalties add +1 to this.
    /// </summary>
    public readonly record struct CycleInfo(int BaseCycles, bool PageCrossPenalty);

    /// <summary>
    /// Comprehensive cycle lookup table for all 6502 instructions by addressing mode and access type.
    /// 
    /// Read cycles: Includes page-cross penalty flag (applied in AddressingMode helpers or ReadWithCycles helper).
    ///   - LDA #$42 (Immediate, Read) = 2 cycles, no penalty (page cross impossible)
    ///   - LDA $42,X (ZeroPageX, Read) = 4 cycles, no penalty (wraps within zero page)
    ///   - LDA $1234,X (AbsoluteX, Read) = 4 cycles + 1 if page cross
    /// 
    /// Write cycles: No page-cross penalty applies; base always includes full cost.
    ///   - STA $42 (ZeroPage, Write) = 3 cycles
    ///   - STA $1234,X (AbsoluteX, Write) = 5 cycles (always 5, indexed writes don't cross penalty)
    /// 
    /// RMW cycles: Base always includes page-cross overhead; no penalty flag.
    ///   - INC $42 (ZeroPage, Rmw) = 5 cycles
    ///   - INC $1234,X (AbsoluteX, Rmw) = 7 cycles (always 7, includes page cross if applicable)
    /// </summary>
    private static readonly Dictionary<(AddressingMode, AccessType), CycleInfo> CycleTable = new()
    {
        // ── Immediate (no page cross possible; always 2 base) ─────────────────────
        { (AddressingMode.Immediate, AccessType.Read),  new(2, false) },

        // ── Zero Page (no page cross possible within zero page; fixed count) ──────
        { (AddressingMode.ZeroPage,   AccessType.Read),  new(3, false) },
        { (AddressingMode.ZeroPage,   AccessType.Write), new(3, false) },
        { (AddressingMode.ZeroPage,   AccessType.Rmw),   new(5, false) },

        // ── Zero Page, X (wraps within zero page; fixed count) ───────────────────
        { (AddressingMode.ZeroPageX,  AccessType.Read),  new(4, false) },
        { (AddressingMode.ZeroPageX,  AccessType.Write), new(4, false) },
        { (AddressingMode.ZeroPageX,  AccessType.Rmw),   new(6, false) },

        // ── Zero Page, Y (wraps within zero page; fixed count; only LDX, STX) ───
        { (AddressingMode.ZeroPageY,  AccessType.Read),  new(4, false) },
        { (AddressingMode.ZeroPageY,  AccessType.Write), new(4, false) },

        // ── Absolute (no page cross possible; fixed count) ──────────────────────
        { (AddressingMode.Absolute,   AccessType.Read),  new(4, false) },
        { (AddressingMode.Absolute,   AccessType.Write), new(4, false) },
        { (AddressingMode.Absolute,   AccessType.Rmw),   new(6, false) },

        // ── Absolute, X (page cross penalty possible on read; write bakes cost) ──
        { (AddressingMode.AbsoluteX,  AccessType.Read),  new(4, true) },   // 4 base, +1 if page cross
        { (AddressingMode.AbsoluteX,  AccessType.Write), new(5, false) },  // always 5 (includes page cross overhead)
        { (AddressingMode.AbsoluteX,  AccessType.Rmw),   new(7, false) },  // always 7 (includes page cross overhead)

        // ── Absolute, Y (page cross penalty possible on read; write bakes cost) ──
        { (AddressingMode.AbsoluteY,  AccessType.Read),  new(4, true) },   // 4 base, +1 if page cross
        { (AddressingMode.AbsoluteY,  AccessType.Write), new(5, false) },  // always 5 (includes page cross overhead)
        { (AddressingMode.AbsoluteY,  AccessType.Rmw),   new(7, false) },  // always 7 (includes page cross overhead)

        // ── Indexed Indirect ($zp,X) (no page cross overhead; fixed cost) ────────
        { (AddressingMode.IndirectX,  AccessType.Read),  new(6, false) },
        { (AddressingMode.IndirectX,  AccessType.Write), new(6, false) },
        { (AddressingMode.IndirectX,  AccessType.Rmw),   new(8, false) },

        // ── Indirect Indexed ($zp),Y (page cross penalty possible on read) ──────
        { (AddressingMode.IndirectY,  AccessType.Read),  new(5, true) },   // 5 base, +1 if page cross
        { (AddressingMode.IndirectY,  AccessType.Write), new(6, false) },  // always 6
        { (AddressingMode.IndirectY,  AccessType.Rmw),   new(8, false) },  // always 8

        // ── Indirect (JMP only; no page cross penalty) ────────────────────────────
        { (AddressingMode.Indirect,   AccessType.Read),  new(5, false) },

        // ── Relative (Branches; page cross penalty applies on branch taken) ──────
        { (AddressingMode.Relative,   AccessType.Read),  new(2, true) },   // 2 base, +1 if branch taken, +2 if page cross
    };

    /// <summary>
    /// Retrieve cycle information for a given addressing mode and access type.
    /// </summary>
    private static CycleInfo GetCycleInfo(AddressingMode mode, AccessType access)
    {
        if (CycleTable.TryGetValue((mode, access), out var info))
            return info;
        
        throw new InvalidOperationException(
            $"No cycle metadata for {mode} + {access}. Check CycleTable.");
    }
}
