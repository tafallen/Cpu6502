namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── CMP ───────────────────────────────────────────────────────────────────
    private void CMP_Imm()  { DoCMP(A, ReadByte(AddrImmediate()));                TotalCycles += 2; }
    private void CMP_Zp()   { DoCMP(A, ReadByte(AddrZeroPage()));                 TotalCycles += 3; }
    private void CMP_ZpX()  { DoCMP(A, ReadByte(AddrZeroPageX()));                TotalCycles += 4; }
    private void CMP_Abs()  { DoCMP(A, ReadByte(AddrAbsolute()));                 TotalCycles += 4; }
    private void CMP_AbsX() { DoCMP(A, ReadByte(AddrAbsoluteX()));                TotalCycles += 4; }
    private void CMP_AbsY() { DoCMP(A, ReadByte(AddrAbsoluteY()));                TotalCycles += 4; }
    private void CMP_IndX() { DoCMP(A, ReadByte(AddrIndexedIndirect()));          TotalCycles += 6; }
    private void CMP_IndY() { DoCMP(A, ReadByte(AddrIndirectIndexed()));          TotalCycles += 5; }

    // ── CPX ───────────────────────────────────────────────────────────────────
    private void CPX_Imm()  { DoCMP(X, ReadByte(AddrImmediate()));                TotalCycles += 2; }
    private void CPX_Zp()   { DoCMP(X, ReadByte(AddrZeroPage()));                 TotalCycles += 3; }
    private void CPX_Abs()  { DoCMP(X, ReadByte(AddrAbsolute()));                 TotalCycles += 4; }

    // ── CPY ───────────────────────────────────────────────────────────────────
    private void CPY_Imm()  { DoCMP(Y, ReadByte(AddrImmediate()));                TotalCycles += 2; }
    private void CPY_Zp()   { DoCMP(Y, ReadByte(AddrZeroPage()));                 TotalCycles += 3; }
    private void CPY_Abs()  { DoCMP(Y, ReadByte(AddrAbsolute()));                 TotalCycles += 4; }

    private void DoCMP(byte reg, byte val)
    {
        int result = reg - val;
        C = reg >= val;
        Z = result == 0;
        N = (result & 0x80) != 0;
    }
}
