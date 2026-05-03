namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── CMP ───────────────────────────────────────────────────────────────────
    private void CMP_Imm()  { DoCMP(A, ReadByte(AddrImmediate()));                TotalCycles += (ulong)GetCycleInfo(AddressingMode.Immediate, AccessType.Read).BaseCycles; }
    private void CMP_Zp()   { DoCMP(A, ReadByte(AddrZeroPage()));                 TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void CMP_ZpX()  { DoCMP(A, ReadByte(AddrZeroPageX()));                TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Read).BaseCycles; }
    private void CMP_Abs()  { DoCMP(A, ReadByte(AddrAbsolute()));                 TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }
    private void CMP_AbsX() { DoCMP(A, ReadByte(AddrAbsoluteX()));                TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Read).BaseCycles; }
    private void CMP_AbsY() { DoCMP(A, ReadByte(AddrAbsoluteY()));                TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteY, AccessType.Read).BaseCycles; }
    private void CMP_IndX() { DoCMP(A, ReadByte(AddrIndexedIndirect()));          TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectX, AccessType.Read).BaseCycles; }
    private void CMP_IndY() { DoCMP(A, ReadByte(AddrIndirectIndexed()));          TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectY, AccessType.Read).BaseCycles; }

    // ── CPX ───────────────────────────────────────────────────────────────────
    private void CPX_Imm()  { DoCMP(X, ReadByte(AddrImmediate()));                TotalCycles += (ulong)GetCycleInfo(AddressingMode.Immediate, AccessType.Read).BaseCycles; }
    private void CPX_Zp()   { DoCMP(X, ReadByte(AddrZeroPage()));                 TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void CPX_Abs()  { DoCMP(X, ReadByte(AddrAbsolute()));                 TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }

    // ── CPY ───────────────────────────────────────────────────────────────────
    private void CPY_Imm()  { DoCMP(Y, ReadByte(AddrImmediate()));                TotalCycles += (ulong)GetCycleInfo(AddressingMode.Immediate, AccessType.Read).BaseCycles; }
    private void CPY_Zp()   { DoCMP(Y, ReadByte(AddrZeroPage()));                 TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void CPY_Abs()  { DoCMP(Y, ReadByte(AddrAbsolute()));                 TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }

    private void DoCMP(byte reg, byte val)
    {
        int result = reg - val;
        C = reg >= val;
        Z = result == 0;
        N = (result & 0x80) != 0;
    }
}
