namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── LDA ───────────────────────────────────────────────────────────────────
    private void LDA_Imm()  { A = ReadByte(AddrImmediate());                        SetZN(A); TotalCycles += 2; }
    private void LDA_Zp()   { A = ReadByte(AddrZeroPage());                         SetZN(A); TotalCycles += 3; }
    private void LDA_ZpX()  { A = ReadByte(AddrZeroPageX());                        SetZN(A); TotalCycles += 4; }
    private void LDA_Abs()  { A = ReadByte(AddrAbsolute());                         SetZN(A); TotalCycles += 4; }
    private void LDA_AbsX() { A = ReadByte(AddrAbsoluteX());                        SetZN(A); TotalCycles += 4; }
    private void LDA_AbsY() { A = ReadByte(AddrAbsoluteY());                        SetZN(A); TotalCycles += 4; }
    private void LDA_IndX() { A = ReadByte(AddrIndexedIndirect());                  SetZN(A); TotalCycles += 6; }
    private void LDA_IndY() { A = ReadByte(AddrIndirectIndexed());                  SetZN(A); TotalCycles += 5; }

    // ── LDX ───────────────────────────────────────────────────────────────────
    private void LDX_Imm()  { X = ReadByte(AddrImmediate());                        SetZN(X); TotalCycles += 2; }
    private void LDX_Zp()   { X = ReadByte(AddrZeroPage());                         SetZN(X); TotalCycles += 3; }
    private void LDX_ZpY()  { X = ReadByte(AddrZeroPageY());                        SetZN(X); TotalCycles += 4; }
    private void LDX_Abs()  { X = ReadByte(AddrAbsolute());                         SetZN(X); TotalCycles += 4; }
    private void LDX_AbsY() { X = ReadByte(AddrAbsoluteY());                        SetZN(X); TotalCycles += 4; }

    // ── LDY ───────────────────────────────────────────────────────────────────
    private void LDY_Imm()  { Y = ReadByte(AddrImmediate());                        SetZN(Y); TotalCycles += 2; }
    private void LDY_Zp()   { Y = ReadByte(AddrZeroPage());                         SetZN(Y); TotalCycles += 3; }
    private void LDY_ZpX()  { Y = ReadByte(AddrZeroPageX());                        SetZN(Y); TotalCycles += 4; }
    private void LDY_Abs()  { Y = ReadByte(AddrAbsolute());                         SetZN(Y); TotalCycles += 4; }
    private void LDY_AbsX() { Y = ReadByte(AddrAbsoluteX());                        SetZN(Y); TotalCycles += 4; }

    // ── STA ───────────────────────────────────────────────────────────────────
    private void STA_Zp()   { WriteByte(AddrZeroPage(),                         A); TotalCycles += 3; }
    private void STA_ZpX()  { WriteByte(AddrZeroPageX(),                        A); TotalCycles += 4; }
    private void STA_Abs()  { WriteByte(AddrAbsolute(),                         A); TotalCycles += 4; }
    private void STA_AbsX() { WriteByte(AddrAbsoluteX(), A); TotalCycles += 5; }  // always 5, no page-cross penalty on writes
    private void STA_AbsY() { WriteByte(AddrAbsoluteY(), A); TotalCycles += 5; }
    private void STA_IndX() { WriteByte(AddrIndexedIndirect(), A); TotalCycles += 6; }
    private void STA_IndY() { WriteByte(AddrIndirectIndexed(), A); TotalCycles += 6; }

    // ── STX ───────────────────────────────────────────────────────────────────
    private void STX_Zp()   { WriteByte(AddrZeroPage(),  X); TotalCycles += 3; }
    private void STX_ZpY()  { WriteByte(AddrZeroPageY(), X); TotalCycles += 4; }
    private void STX_Abs()  { WriteByte(AddrAbsolute(),  X); TotalCycles += 4; }

    // ── STY ───────────────────────────────────────────────────────────────────
    private void STY_Zp()   { WriteByte(AddrZeroPage(),  Y); TotalCycles += 3; }
    private void STY_ZpX()  { WriteByte(AddrZeroPageX(), Y); TotalCycles += 4; }
    private void STY_Abs()  { WriteByte(AddrAbsolute(),  Y); TotalCycles += 4; }
}
