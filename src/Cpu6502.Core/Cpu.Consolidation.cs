namespace Cpu6502.Core;

/// <summary>
/// Generic instruction execution helpers to consolidate nearly identical instruction handlers.
/// These methods reduce code duplication by ~30-40% and provide a single point for logic changes.
/// </summary>
public sealed partial class Cpu
{
    // ─────────────────────────────────────────────────────────────────────────
    // Generic Load/Store Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generic load operation: Read byte from address, set Z/N flags, add cycles.
    /// Consolidates LDA_*, LDX_*, LDY_* methods.
    /// Returns the loaded value so it can be assigned to the target register via lambda.
    /// </summary>
    private byte ExecuteLoad(AddressingMode mode)
    {
        byte result = ReadByte(GetAddressByMode(mode));
        SetZN(result);
        TotalCycles += (ulong)GetCycleInfo(mode, AccessType.Read).BaseCycles;
        return result;
    }

    /// <summary>
    /// Generic store operation: Write value to address, add cycles.
    /// Consolidates STA_*, STX_*, STY_* methods.
    /// </summary>
    private void ExecuteStore(byte value, AddressingMode mode)
    {
        WriteByte(GetAddressByMode(mode), value);
        TotalCycles += (ulong)GetCycleInfo(mode, AccessType.Write).BaseCycles;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Generic Logic Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generic logic operation: Read byte, apply operation (AND/ORA/EOR), set Z/N flags, add cycles.
    /// Consolidates AND_*, ORA_*, EOR_* methods.
    /// Returns the result and updates the accumulator within the lambda.
    /// </summary>
    private byte ExecuteLogic(LogicOp operation, AddressingMode mode)
    {
        byte val = ReadByte(GetAddressByMode(mode));
        byte result = operation switch
        {
            LogicOp.AND => (byte)(A & val),
            LogicOp.ORA => (byte)(A | val),
            LogicOp.EOR => (byte)(A ^ val),
            _ => throw new InvalidOperationException($"Unknown logic operation: {operation}")
        };
        SetZN(result);
        TotalCycles += (ulong)GetCycleInfo(mode, AccessType.Read).BaseCycles;
        return result;
    }

    /// <summary>
    /// BIT operation: Test bits, set Z/N/V flags, add cycles.
    /// Separate from ExecuteLogic because it doesn't store the result in A.
    /// </summary>
    private void ExecuteBit(AddressingMode mode)
    {
        byte val = ReadByte(GetAddressByMode(mode));
        Z = (A & val) == 0;
        N = (val & CpuConstants.BIT_7_MASK) != 0;
        V = (val & CpuConstants.BIT_6_MASK) != 0;
        TotalCycles += (ulong)GetCycleInfo(mode, AccessType.Read).BaseCycles;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Generic Arithmetic Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generic arithmetic operation: Read byte, apply ADC or SBC, add cycles.
    /// Consolidates ADC_* and SBC_* methods (which delegate to AdcCore/SbcCore).
    /// Note: A is modified in place by AdcCore/SbcCore, so no return value needed.
    /// </summary>
    private void ExecuteArithmetic(ArithmeticOp operation, AddressingMode mode)
    {
        byte val = ReadByte(GetAddressByMode(mode));
        if (operation == ArithmeticOp.ADC)
            AdcCore(val);
        else if (operation == ArithmeticOp.SBC)
            SbcCore(val);
        else
            throw new InvalidOperationException($"Unknown arithmetic operation: {operation}");
        TotalCycles += (ulong)GetCycleInfo(mode, AccessType.Read).BaseCycles;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Generic Shift Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generic shift operation on accumulator: Apply shift operation to A, add cycles.
    /// Consolidates ASL_Acc, LSR_Acc, ROL_Acc, ROR_Acc methods.
    /// </summary>
    private void ExecuteShiftAccumulator(ShiftOp operation)
    {
        A = operation switch
        {
            ShiftOp.ASL => DoASL(A),
            ShiftOp.LSR => DoLSR(A),
            ShiftOp.ROL => DoROL(A),
            ShiftOp.ROR => DoROR(A),
            _ => throw new InvalidOperationException($"Unknown shift operation: {operation}")
        };
        TotalCycles += 2;
    }

    /// <summary>
    /// Generic shift operation on memory: Read, apply operation, write back, add cycles.
    /// Consolidates ASL_Zp, ASL_ZpX, ASL_Abs, ASL_AbsX, and same for LSR/ROL/ROR.
    /// </summary>
    private void ExecuteShiftMemory(ShiftOp operation, AddressingMode mode)
    {
        ushort addr = GetAddressByMode(mode);
        byte val = ReadByte(addr);
        byte result = operation switch
        {
            ShiftOp.ASL => DoASL(val),
            ShiftOp.LSR => DoLSR(val),
            ShiftOp.ROL => DoROL(val),
            ShiftOp.ROR => DoROR(val),
            _ => throw new InvalidOperationException($"Unknown shift operation: {operation}")
        };
        WriteByte(addr, result);
        TotalCycles += (ulong)GetCycleInfo(mode, AccessType.Rmw).BaseCycles;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Address Mode Router
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Routes to the appropriate addressing mode method and returns the calculated address.
    /// Central point for all addressing mode logic.
    /// </summary>
    private ushort GetAddressByMode(AddressingMode mode) => mode switch
    {
        AddressingMode.Immediate => AddrImmediate(),
        AddressingMode.ZeroPage => AddrZeroPage(),
        AddressingMode.ZeroPageX => AddrZeroPageX(),
        AddressingMode.ZeroPageY => AddrZeroPageY(),
        AddressingMode.Absolute => AddrAbsolute(),
        AddressingMode.AbsoluteX => AddrAbsoluteX(),
        AddressingMode.AbsoluteY => AddrAbsoluteY(),
        AddressingMode.IndirectX => AddrIndexedIndirect(),
        AddressingMode.IndirectY => AddrIndirectIndexed(),
        AddressingMode.Indirect => throw new InvalidOperationException("Indirect addressing not supported by generalized handlers (JMP only)"),
        AddressingMode.Relative => throw new InvalidOperationException("Relative addressing handled by branch instructions directly"),
        _ => throw new InvalidOperationException($"Unknown addressing mode: {mode}")
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Generic Compare Helper
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generic compare operation: Read byte, compare with register, set C/Z/N flags, add cycles.
    /// Consolidates CMP_*, CPX_*, CPY_* methods (13 total).
    /// </summary>
    private void ExecuteCompare(byte register, AddressingMode mode)
    {
        byte val = ReadByte(GetAddressByMode(mode));
        DoCMP(register, val);
        TotalCycles += (ulong)GetCycleInfo(mode, AccessType.Read).BaseCycles;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Generic Read-Modify-Write Helper
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generic RMW operation: Read byte, apply operation (INC/DEC), write back, set Z/N, add cycles.
    /// Consolidates INC_* and DEC_* memory methods (8 total).
    /// </summary>
    private void ExecuteRMW(Func<byte, byte> operation, AddressingMode mode)
    {
        ushort addr = GetAddressByMode(mode);
        byte result = operation(ReadByte(addr));
        WriteByte(addr, result);
        SetZN(result);
        TotalCycles += (ulong)GetCycleInfo(mode, AccessType.Rmw).BaseCycles;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Illegal Opcode RMW Helpers (compound operations)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generic illegal opcode helper: Read, apply operation, write, set Z/N, add cycles.
    /// Consolidates DCP, ISB, SLO, RLA, SRE, RRA methods (42 total across 7 addressing modes each).
    /// Operation parameter receives byte value and returns modified value.
    /// FlagOp parameter (if provided) runs after the write (for flag updates specific to each illegal operation).
    /// </summary>
    private void ExecuteIllegalRMW(Func<byte, byte> operation, Action<byte>? flagOp, AddressingMode mode)
    {
        ushort addr = GetAddressByMode(mode);
        byte val = ReadByte(addr);
        byte result = operation(val);
        WriteByte(addr, result);
        flagOp?.Invoke(result);  // Apply any post-operation flag updates
        TotalCycles += (ulong)GetCycleInfo(mode, AccessType.Rmw).BaseCycles;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Operation Enums
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Logic operations: AND, ORA (OR), EOR (XOR).</summary>
    private enum LogicOp { AND, ORA, EOR }

    /// <summary>Arithmetic operations: ADC (Add with Carry), SBC (Subtract with Carry).</summary>
    private enum ArithmeticOp { ADC, SBC }

    /// <summary>Shift operations: ASL, LSR, ROL, ROR.</summary>
    private enum ShiftOp { ASL, LSR, ROL, ROR }

    // ─────────────────────────────────────────────────────────────────────────
    // Dispatch Table Wrapper Methods (Option C: method references)
    // ─────────────────────────────────────────────────────────────────────────
    // These wrapper methods allow lambda closures to be converted to direct method
    // references in BuildDispatchTable, avoiding heap allocations and improving JIT inlining.

    // ── NOP ───────────────────────────────────────────────────────────────────
    private void NOP_Implied() { TotalCycles += 2; }

    // ── Load A (LDA) ──────────────────────────────────────────────────────────
    private void LDA_Immediate() => A = ExecuteLoad(AddressingMode.Immediate);
    private void LDA_ZeroPage() => A = ExecuteLoad(AddressingMode.ZeroPage);
    private void LDA_ZeroPageX() => A = ExecuteLoad(AddressingMode.ZeroPageX);
    private void LDA_Absolute() => A = ExecuteLoad(AddressingMode.Absolute);
    private void LDA_AbsoluteX() => A = ExecuteLoad(AddressingMode.AbsoluteX);
    private void LDA_AbsoluteY() => A = ExecuteLoad(AddressingMode.AbsoluteY);
    private void LDA_IndirectX() => A = ExecuteLoad(AddressingMode.IndirectX);
    private void LDA_IndirectY() => A = ExecuteLoad(AddressingMode.IndirectY);

    // ── Load X (LDX) ──────────────────────────────────────────────────────────
    private void LDX_Immediate() => X = ExecuteLoad(AddressingMode.Immediate);
    private void LDX_ZeroPage() => X = ExecuteLoad(AddressingMode.ZeroPage);
    private void LDX_ZeroPageY() => X = ExecuteLoad(AddressingMode.ZeroPageY);
    private void LDX_Absolute() => X = ExecuteLoad(AddressingMode.Absolute);
    private void LDX_AbsoluteY() => X = ExecuteLoad(AddressingMode.AbsoluteY);

    // ── Load Y (LDY) ──────────────────────────────────────────────────────────
    private void LDY_Immediate() => Y = ExecuteLoad(AddressingMode.Immediate);
    private void LDY_ZeroPage() => Y = ExecuteLoad(AddressingMode.ZeroPage);
    private void LDY_ZeroPageX() => Y = ExecuteLoad(AddressingMode.ZeroPageX);
    private void LDY_Absolute() => Y = ExecuteLoad(AddressingMode.Absolute);
    private void LDY_AbsoluteX() => Y = ExecuteLoad(AddressingMode.AbsoluteX);

    // ── Store A (STA) ─────────────────────────────────────────────────────────
    private void STA_ZeroPage() => ExecuteStore(A, AddressingMode.ZeroPage);
    private void STA_ZeroPageX() => ExecuteStore(A, AddressingMode.ZeroPageX);
    private void STA_Absolute() => ExecuteStore(A, AddressingMode.Absolute);
    private void STA_AbsoluteX() => ExecuteStore(A, AddressingMode.AbsoluteX);
    private void STA_AbsoluteY() => ExecuteStore(A, AddressingMode.AbsoluteY);
    private void STA_IndirectX() => ExecuteStore(A, AddressingMode.IndirectX);
    private void STA_IndirectY() => ExecuteStore(A, AddressingMode.IndirectY);

    // ── Store X (STX) ─────────────────────────────────────────────────────────
    private void STX_ZeroPage() => ExecuteStore(X, AddressingMode.ZeroPage);
    private void STX_ZeroPageY() => ExecuteStore(X, AddressingMode.ZeroPageY);
    private void STX_Absolute() => ExecuteStore(X, AddressingMode.Absolute);

    // ── Store Y (STY) ─────────────────────────────────────────────────────────
    private void STY_ZeroPage() => ExecuteStore(Y, AddressingMode.ZeroPage);
    private void STY_ZeroPageX() => ExecuteStore(Y, AddressingMode.ZeroPageX);
    private void STY_Absolute() => ExecuteStore(Y, AddressingMode.Absolute);

    // ── Arithmetic ADC ────────────────────────────────────────────────────────
    private void ADC_Immediate() => ExecuteArithmetic(ArithmeticOp.ADC, AddressingMode.Immediate);
    private void ADC_ZeroPage() => ExecuteArithmetic(ArithmeticOp.ADC, AddressingMode.ZeroPage);
    private void ADC_ZeroPageX() => ExecuteArithmetic(ArithmeticOp.ADC, AddressingMode.ZeroPageX);
    private void ADC_Absolute() => ExecuteArithmetic(ArithmeticOp.ADC, AddressingMode.Absolute);
    private void ADC_AbsoluteX() => ExecuteArithmetic(ArithmeticOp.ADC, AddressingMode.AbsoluteX);
    private void ADC_AbsoluteY() => ExecuteArithmetic(ArithmeticOp.ADC, AddressingMode.AbsoluteY);
    private void ADC_IndirectX() => ExecuteArithmetic(ArithmeticOp.ADC, AddressingMode.IndirectX);
    private void ADC_IndirectY() => ExecuteArithmetic(ArithmeticOp.ADC, AddressingMode.IndirectY);

    // ── Arithmetic SBC ────────────────────────────────────────────────────────
    private void SBC_Immediate() => ExecuteArithmetic(ArithmeticOp.SBC, AddressingMode.Immediate);
    private void SBC_ZeroPage() => ExecuteArithmetic(ArithmeticOp.SBC, AddressingMode.ZeroPage);
    private void SBC_ZeroPageX() => ExecuteArithmetic(ArithmeticOp.SBC, AddressingMode.ZeroPageX);
    private void SBC_Absolute() => ExecuteArithmetic(ArithmeticOp.SBC, AddressingMode.Absolute);
    private void SBC_AbsoluteX() => ExecuteArithmetic(ArithmeticOp.SBC, AddressingMode.AbsoluteX);
    private void SBC_AbsoluteY() => ExecuteArithmetic(ArithmeticOp.SBC, AddressingMode.AbsoluteY);
    private void SBC_IndirectX() => ExecuteArithmetic(ArithmeticOp.SBC, AddressingMode.IndirectX);
    private void SBC_IndirectY() => ExecuteArithmetic(ArithmeticOp.SBC, AddressingMode.IndirectY);

    // ── Increment/Decrement Memory ────────────────────────────────────────────
    private void INC_ZeroPage() => ExecuteRMW(v => (byte)(v + 1), AddressingMode.ZeroPage);
    private void INC_ZeroPageX() => ExecuteRMW(v => (byte)(v + 1), AddressingMode.ZeroPageX);
    private void INC_Absolute() => ExecuteRMW(v => (byte)(v + 1), AddressingMode.Absolute);
    private void INC_AbsoluteX() => ExecuteRMW(v => (byte)(v + 1), AddressingMode.AbsoluteX);

    private void DEC_ZeroPage() => ExecuteRMW(v => (byte)(v - 1), AddressingMode.ZeroPage);
    private void DEC_ZeroPageX() => ExecuteRMW(v => (byte)(v - 1), AddressingMode.ZeroPageX);
    private void DEC_Absolute() => ExecuteRMW(v => (byte)(v - 1), AddressingMode.Absolute);
    private void DEC_AbsoluteX() => ExecuteRMW(v => (byte)(v - 1), AddressingMode.AbsoluteX);

    // ── Logic AND ─────────────────────────────────────────────────────────────
    private void AND_Immediate() => A = ExecuteLogic(LogicOp.AND, AddressingMode.Immediate);
    private void AND_ZeroPage() => A = ExecuteLogic(LogicOp.AND, AddressingMode.ZeroPage);
    private void AND_ZeroPageX() => A = ExecuteLogic(LogicOp.AND, AddressingMode.ZeroPageX);
    private void AND_Absolute() => A = ExecuteLogic(LogicOp.AND, AddressingMode.Absolute);
    private void AND_AbsoluteX() => A = ExecuteLogic(LogicOp.AND, AddressingMode.AbsoluteX);
    private void AND_AbsoluteY() => A = ExecuteLogic(LogicOp.AND, AddressingMode.AbsoluteY);
    private void AND_IndirectX() => A = ExecuteLogic(LogicOp.AND, AddressingMode.IndirectX);
    private void AND_IndirectY() => A = ExecuteLogic(LogicOp.AND, AddressingMode.IndirectY);

    // ── Logic ORA ─────────────────────────────────────────────────────────────
    private void ORA_Immediate() => A = ExecuteLogic(LogicOp.ORA, AddressingMode.Immediate);
    private void ORA_ZeroPage() => A = ExecuteLogic(LogicOp.ORA, AddressingMode.ZeroPage);
    private void ORA_ZeroPageX() => A = ExecuteLogic(LogicOp.ORA, AddressingMode.ZeroPageX);
    private void ORA_Absolute() => A = ExecuteLogic(LogicOp.ORA, AddressingMode.Absolute);
    private void ORA_AbsoluteX() => A = ExecuteLogic(LogicOp.ORA, AddressingMode.AbsoluteX);
    private void ORA_AbsoluteY() => A = ExecuteLogic(LogicOp.ORA, AddressingMode.AbsoluteY);
    private void ORA_IndirectX() => A = ExecuteLogic(LogicOp.ORA, AddressingMode.IndirectX);
    private void ORA_IndirectY() => A = ExecuteLogic(LogicOp.ORA, AddressingMode.IndirectY);

    // ── Logic EOR ─────────────────────────────────────────────────────────────
    private void EOR_Immediate() => A = ExecuteLogic(LogicOp.EOR, AddressingMode.Immediate);
    private void EOR_ZeroPage() => A = ExecuteLogic(LogicOp.EOR, AddressingMode.ZeroPage);
    private void EOR_ZeroPageX() => A = ExecuteLogic(LogicOp.EOR, AddressingMode.ZeroPageX);
    private void EOR_Absolute() => A = ExecuteLogic(LogicOp.EOR, AddressingMode.Absolute);
    private void EOR_AbsoluteX() => A = ExecuteLogic(LogicOp.EOR, AddressingMode.AbsoluteX);
    private void EOR_AbsoluteY() => A = ExecuteLogic(LogicOp.EOR, AddressingMode.AbsoluteY);
    private void EOR_IndirectX() => A = ExecuteLogic(LogicOp.EOR, AddressingMode.IndirectX);
    private void EOR_IndirectY() => A = ExecuteLogic(LogicOp.EOR, AddressingMode.IndirectY);

    // ── Bit Test ──────────────────────────────────────────────────────────────
    private void BIT_ZeroPage() => ExecuteBit(AddressingMode.ZeroPage);
    private void BIT_Absolute() => ExecuteBit(AddressingMode.Absolute);

    // ── Shifts/Rotates Accumulator ────────────────────────────────────────────
    private void ASL_Accumulator() => ExecuteShiftAccumulator(ShiftOp.ASL);
    private void LSR_Accumulator() => ExecuteShiftAccumulator(ShiftOp.LSR);
    private void ROL_Accumulator() => ExecuteShiftAccumulator(ShiftOp.ROL);
    private void ROR_Accumulator() => ExecuteShiftAccumulator(ShiftOp.ROR);

    // ── Shifts/Rotates Memory (ASL) ───────────────────────────────────────────
    private void ASL_ZeroPage() => ExecuteShiftMemory(ShiftOp.ASL, AddressingMode.ZeroPage);
    private void ASL_ZeroPageX() => ExecuteShiftMemory(ShiftOp.ASL, AddressingMode.ZeroPageX);
    private void ASL_Absolute() => ExecuteShiftMemory(ShiftOp.ASL, AddressingMode.Absolute);
    private void ASL_AbsoluteX() => ExecuteShiftMemory(ShiftOp.ASL, AddressingMode.AbsoluteX);

    // ── Shifts/Rotates Memory (LSR) ───────────────────────────────────────────
    private void LSR_ZeroPage() => ExecuteShiftMemory(ShiftOp.LSR, AddressingMode.ZeroPage);
    private void LSR_ZeroPageX() => ExecuteShiftMemory(ShiftOp.LSR, AddressingMode.ZeroPageX);
    private void LSR_Absolute() => ExecuteShiftMemory(ShiftOp.LSR, AddressingMode.Absolute);
    private void LSR_AbsoluteX() => ExecuteShiftMemory(ShiftOp.LSR, AddressingMode.AbsoluteX);

    // ── Shifts/Rotates Memory (ROL) ───────────────────────────────────────────
    private void ROL_ZeroPage() => ExecuteShiftMemory(ShiftOp.ROL, AddressingMode.ZeroPage);
    private void ROL_ZeroPageX() => ExecuteShiftMemory(ShiftOp.ROL, AddressingMode.ZeroPageX);
    private void ROL_Absolute() => ExecuteShiftMemory(ShiftOp.ROL, AddressingMode.Absolute);
    private void ROL_AbsoluteX() => ExecuteShiftMemory(ShiftOp.ROL, AddressingMode.AbsoluteX);

    // ── Shifts/Rotates Memory (ROR) ───────────────────────────────────────────
    private void ROR_ZeroPage() => ExecuteShiftMemory(ShiftOp.ROR, AddressingMode.ZeroPage);
    private void ROR_ZeroPageX() => ExecuteShiftMemory(ShiftOp.ROR, AddressingMode.ZeroPageX);
    private void ROR_Absolute() => ExecuteShiftMemory(ShiftOp.ROR, AddressingMode.Absolute);
    private void ROR_AbsoluteX() => ExecuteShiftMemory(ShiftOp.ROR, AddressingMode.AbsoluteX);

    // ── Compare A (CMP) ───────────────────────────────────────────────────────
    private void CMP_Immediate() => ExecuteCompare(A, AddressingMode.Immediate);
    private void CMP_ZeroPage() => ExecuteCompare(A, AddressingMode.ZeroPage);
    private void CMP_ZeroPageX() => ExecuteCompare(A, AddressingMode.ZeroPageX);
    private void CMP_Absolute() => ExecuteCompare(A, AddressingMode.Absolute);
    private void CMP_AbsoluteX() => ExecuteCompare(A, AddressingMode.AbsoluteX);
    private void CMP_AbsoluteY() => ExecuteCompare(A, AddressingMode.AbsoluteY);
    private void CMP_IndirectX() => ExecuteCompare(A, AddressingMode.IndirectX);
    private void CMP_IndirectY() => ExecuteCompare(A, AddressingMode.IndirectY);

    // ── Compare X (CPX) ───────────────────────────────────────────────────────
    private void CPX_Immediate() => ExecuteCompare(X, AddressingMode.Immediate);
    private void CPX_ZeroPage() => ExecuteCompare(X, AddressingMode.ZeroPage);
    private void CPX_Absolute() => ExecuteCompare(X, AddressingMode.Absolute);

    // ── Compare Y (CPY) ───────────────────────────────────────────────────────
    private void CPY_Immediate() => ExecuteCompare(Y, AddressingMode.Immediate);
    private void CPY_ZeroPage() => ExecuteCompare(Y, AddressingMode.ZeroPage);
    private void CPY_Absolute() => ExecuteCompare(Y, AddressingMode.Absolute);
}
