namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── LDA ───────────────────────────────────────────────────────────────────
    private void LDA_Imm()  { A = ReadByte(AddrImmediate());                        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Immediate, AccessType.Read).BaseCycles; }
    private void LDA_Zp()   { A = ReadByte(AddrZeroPage());                         SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void LDA_ZpX()  { A = ReadByte(AddrZeroPageX());                        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Read).BaseCycles; }
    private void LDA_Abs()  { A = ReadByte(AddrAbsolute());                         SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }
    private void LDA_AbsX() { A = ReadByte(AddrAbsoluteX());                        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Read).BaseCycles; }
    private void LDA_AbsY() { A = ReadByte(AddrAbsoluteY());                        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteY, AccessType.Read).BaseCycles; }
    private void LDA_IndX() { A = ReadByte(AddrIndexedIndirect());                  SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectX, AccessType.Read).BaseCycles; }
    private void LDA_IndY() { A = ReadByte(AddrIndirectIndexed());                  SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectY, AccessType.Read).BaseCycles; }

    // ── LDX ───────────────────────────────────────────────────────────────────
    private void LDX_Imm()  { X = ReadByte(AddrImmediate());                        SetZN(X); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Immediate, AccessType.Read).BaseCycles; }
    private void LDX_Zp()   { X = ReadByte(AddrZeroPage());                         SetZN(X); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void LDX_ZpY()  { X = ReadByte(AddrZeroPageY());                        SetZN(X); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageY, AccessType.Read).BaseCycles; }
    private void LDX_Abs()  { X = ReadByte(AddrAbsolute());                         SetZN(X); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }
    private void LDX_AbsY() { X = ReadByte(AddrAbsoluteY());                        SetZN(X); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteY, AccessType.Read).BaseCycles; }

    // ── LDY ───────────────────────────────────────────────────────────────────
    private void LDY_Imm()  { Y = ReadByte(AddrImmediate());                        SetZN(Y); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Immediate, AccessType.Read).BaseCycles; }
    private void LDY_Zp()   { Y = ReadByte(AddrZeroPage());                         SetZN(Y); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void LDY_ZpX()  { Y = ReadByte(AddrZeroPageX());                        SetZN(Y); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Read).BaseCycles; }
    private void LDY_Abs()  { Y = ReadByte(AddrAbsolute());                         SetZN(Y); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }
    private void LDY_AbsX() { Y = ReadByte(AddrAbsoluteX());                        SetZN(Y); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Read).BaseCycles; }

    // ── STA ───────────────────────────────────────────────────────────────────
    private void STA_Zp()   { WriteByte(AddrZeroPage(),                         A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Write).BaseCycles; }
    private void STA_ZpX()  { WriteByte(AddrZeroPageX(),                        A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Write).BaseCycles; }
    private void STA_Abs()  { WriteByte(AddrAbsolute(),                         A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Write).BaseCycles; }
    private void STA_AbsX() { WriteByte(AddrAbsoluteX(), A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Write).BaseCycles; }
    private void STA_AbsY() { WriteByte(AddrAbsoluteY(), A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteY, AccessType.Write).BaseCycles; }
    private void STA_IndX() { WriteByte(AddrIndexedIndirect(), A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectX, AccessType.Write).BaseCycles; }
    private void STA_IndY() { WriteByte(AddrIndirectIndexed(), A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectY, AccessType.Write).BaseCycles; }

    // ── STX ───────────────────────────────────────────────────────────────────
    private void STX_Zp()   { WriteByte(AddrZeroPage(),  X); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Write).BaseCycles; }
    private void STX_ZpY()  { WriteByte(AddrZeroPageY(), X); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageY, AccessType.Write).BaseCycles; }
    private void STX_Abs()  { WriteByte(AddrAbsolute(),  X); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Write).BaseCycles; }

    // ── STY ───────────────────────────────────────────────────────────────────
    private void STY_Zp()   { WriteByte(AddrZeroPage(),  Y); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Write).BaseCycles; }
    private void STY_ZpX()  { WriteByte(AddrZeroPageX(), Y); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Write).BaseCycles; }
    private void STY_Abs()  { WriteByte(AddrAbsolute(),  Y); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Write).BaseCycles; }
}
