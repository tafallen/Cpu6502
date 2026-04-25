namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── AND ───────────────────────────────────────────────────────────────────
    private void AND_Imm()  { A &= ReadByte(AddrImmediate());        SetZN(A); TotalCycles += 2; }
    private void AND_Zp()   { A &= ReadByte(AddrZeroPage());         SetZN(A); TotalCycles += 3; }
    private void AND_ZpX()  { A &= ReadByte(AddrZeroPageX());        SetZN(A); TotalCycles += 4; }
    private void AND_Abs()  { A &= ReadByte(AddrAbsolute());         SetZN(A); TotalCycles += 4; }
    private void AND_AbsX() { A &= ReadByte(AddrAbsoluteX());        SetZN(A); TotalCycles += 4; }
    private void AND_AbsY() { A &= ReadByte(AddrAbsoluteY());        SetZN(A); TotalCycles += 4; }
    private void AND_IndX() { A &= ReadByte(AddrIndexedIndirect());  SetZN(A); TotalCycles += 6; }
    private void AND_IndY() { A &= ReadByte(AddrIndirectIndexed());  SetZN(A); TotalCycles += 5; }

    // ── ORA ───────────────────────────────────────────────────────────────────
    private void ORA_Imm()  { A |= ReadByte(AddrImmediate());        SetZN(A); TotalCycles += 2; }
    private void ORA_Zp()   { A |= ReadByte(AddrZeroPage());         SetZN(A); TotalCycles += 3; }
    private void ORA_ZpX()  { A |= ReadByte(AddrZeroPageX());        SetZN(A); TotalCycles += 4; }
    private void ORA_Abs()  { A |= ReadByte(AddrAbsolute());         SetZN(A); TotalCycles += 4; }
    private void ORA_AbsX() { A |= ReadByte(AddrAbsoluteX());        SetZN(A); TotalCycles += 4; }
    private void ORA_AbsY() { A |= ReadByte(AddrAbsoluteY());        SetZN(A); TotalCycles += 4; }
    private void ORA_IndX() { A |= ReadByte(AddrIndexedIndirect());  SetZN(A); TotalCycles += 6; }
    private void ORA_IndY() { A |= ReadByte(AddrIndirectIndexed());  SetZN(A); TotalCycles += 5; }

    // ── EOR ───────────────────────────────────────────────────────────────────
    private void EOR_Imm()  { A ^= ReadByte(AddrImmediate());        SetZN(A); TotalCycles += 2; }
    private void EOR_Zp()   { A ^= ReadByte(AddrZeroPage());         SetZN(A); TotalCycles += 3; }
    private void EOR_ZpX()  { A ^= ReadByte(AddrZeroPageX());        SetZN(A); TotalCycles += 4; }
    private void EOR_Abs()  { A ^= ReadByte(AddrAbsolute());         SetZN(A); TotalCycles += 4; }
    private void EOR_AbsX() { A ^= ReadByte(AddrAbsoluteX());        SetZN(A); TotalCycles += 4; }
    private void EOR_AbsY() { A ^= ReadByte(AddrAbsoluteY());        SetZN(A); TotalCycles += 4; }
    private void EOR_IndX() { A ^= ReadByte(AddrIndexedIndirect());  SetZN(A); TotalCycles += 6; }
    private void EOR_IndY() { A ^= ReadByte(AddrIndirectIndexed());  SetZN(A); TotalCycles += 5; }

    // ── BIT ───────────────────────────────────────────────────────────────────
    private void BIT_Zp()  { DoBIT(ReadByte(AddrZeroPage()));  TotalCycles += 3; }
    private void BIT_Abs() { DoBIT(ReadByte(AddrAbsolute())); TotalCycles += 4; }

    private void DoBIT(byte val)
    {
        Z = (A & val) == 0;
        N = (val & 0x80) != 0;
        V = (val & 0x40) != 0;
    }
}
