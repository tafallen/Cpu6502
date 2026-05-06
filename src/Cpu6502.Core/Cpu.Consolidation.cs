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
    // Operation Enums
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Logic operations: AND, ORA (OR), EOR (XOR).</summary>
    private enum LogicOp { AND, ORA, EOR }

    /// <summary>Arithmetic operations: ADC (Add with Carry), SBC (Subtract with Carry).</summary>
    private enum ArithmeticOp { ADC, SBC }

    /// <summary>Shift operations: ASL, LSR, ROL, ROR.</summary>
    private enum ShiftOp { ASL, LSR, ROL, ROR }
}
