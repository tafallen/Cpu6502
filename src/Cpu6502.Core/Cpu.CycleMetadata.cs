using System.Runtime.CompilerServices;

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
    /// Comprehensive 2D cycle lookup table for all 6502 instructions by addressing mode and access type.
    /// Indexed directly by [(int)mode, (int)access] for O(1) direct array performance.
    /// </summary>
    private static readonly CycleInfo[,] CycleTable = InitCycleTable();

    private static CycleInfo[,] InitCycleTable()
    {
        var table = new CycleInfo[11, 3];

        // ── Immediate (no page cross possible; always 2 base) ─────────────────────
        table[(int)AddressingMode.Immediate, (int)AccessType.Read]  = new(2, false);

        // ── Zero Page (no page cross possible within zero page; fixed count) ──────
        table[(int)AddressingMode.ZeroPage,  (int)AccessType.Read]  = new(3, false);
        table[(int)AddressingMode.ZeroPage,  (int)AccessType.Write] = new(3, false);
        table[(int)AddressingMode.ZeroPage,  (int)AccessType.Rmw]   = new(5, false);

        // ── Zero Page, X (wraps within zero page; fixed count) ───────────────────
        table[(int)AddressingMode.ZeroPageX, (int)AccessType.Read]  = new(4, false);
        table[(int)AddressingMode.ZeroPageX, (int)AccessType.Write] = new(4, false);
        table[(int)AddressingMode.ZeroPageX, (int)AccessType.Rmw]   = new(6, false);

        // ── Zero Page, Y (wraps within zero page; fixed count; only LDX, STX) ───
        table[(int)AddressingMode.ZeroPageY, (int)AccessType.Read]  = new(4, false);
        table[(int)AddressingMode.ZeroPageY, (int)AccessType.Write] = new(4, false);

        // ── Absolute (no page cross possible; fixed count) ──────────────────────
        table[(int)AddressingMode.Absolute,  (int)AccessType.Read]  = new(4, false);
        table[(int)AddressingMode.Absolute,  (int)AccessType.Write] = new(4, false);
        table[(int)AddressingMode.Absolute,  (int)AccessType.Rmw]   = new(6, false);

        // ── Absolute, X (page cross penalty possible on read; write bakes cost) ──
        table[(int)AddressingMode.AbsoluteX, (int)AccessType.Read]  = new(4, true);   // 4 base, +1 if page cross
        table[(int)AddressingMode.AbsoluteX, (int)AccessType.Write] = new(5, false);  // always 5
        table[(int)AddressingMode.AbsoluteX, (int)AccessType.Rmw]   = new(7, false);  // always 7

        // ── Absolute, Y (page cross penalty possible on read; write bakes cost) ──
        table[(int)AddressingMode.AbsoluteY, (int)AccessType.Read]  = new(4, true);   // 4 base, +1 if page cross
        table[(int)AddressingMode.AbsoluteY, (int)AccessType.Write] = new(5, false);  // always 5
        table[(int)AddressingMode.AbsoluteY, (int)AccessType.Rmw]   = new(7, false);  // always 7

        // ── Indexed Indirect ($zp,X) (no page cross overhead; fixed cost) ────────
        table[(int)AddressingMode.IndirectX, (int)AccessType.Read]  = new(6, false);
        table[(int)AddressingMode.IndirectX, (int)AccessType.Write] = new(6, false);
        table[(int)AddressingMode.IndirectX, (int)AccessType.Rmw]   = new(8, false);

        // ── Indirect Indexed ($zp),Y (page cross penalty possible on read) ──────
        table[(int)AddressingMode.IndirectY, (int)AccessType.Read]  = new(5, true);   // 5 base, +1 if page cross
        table[(int)AddressingMode.IndirectY, (int)AccessType.Write] = new(6, false);  // always 6
        table[(int)AddressingMode.IndirectY, (int)AccessType.Rmw]   = new(8, false);  // always 8

        // ── Indirect (JMP only; no page cross penalty) ────────────────────────────
        table[(int)AddressingMode.Indirect,  (int)AccessType.Read]  = new(5, false);

        // ── Relative (Branches; page cross penalty applies on branch taken) ──────
        table[(int)AddressingMode.Relative,  (int)AccessType.Read]  = new(2, true);

        return table;
    }

    /// <summary>
    /// Retrieve cycle information for a given addressing mode and access type.
    /// Fast inlined 2D array lookup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CycleInfo GetCycleInfo(AddressingMode mode, AccessType access)
    {
        return CycleTable[(int)mode, (int)access];
    }
}
