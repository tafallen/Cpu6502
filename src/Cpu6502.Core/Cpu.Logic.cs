namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── AND ───────────────────────────────────────────────────────────────────
    private void AND_Imm()  { A &= ReadByte(AddrImmediate());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Immediate, AccessType.Read).BaseCycles; }
    private void AND_Zp()   { A &= ReadByte(AddrZeroPage());         SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void AND_ZpX()  { A &= ReadByte(AddrZeroPageX());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Read).BaseCycles; }
    private void AND_Abs()  { A &= ReadByte(AddrAbsolute());         SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }
    private void AND_AbsX() { A &= ReadByte(AddrAbsoluteX());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Read).BaseCycles; }
    private void AND_AbsY() { A &= ReadByte(AddrAbsoluteY());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteY, AccessType.Read).BaseCycles; }
    private void AND_IndX() { A &= ReadByte(AddrIndexedIndirect());  SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectX, AccessType.Read).BaseCycles; }
    private void AND_IndY() { A &= ReadByte(AddrIndirectIndexed());  SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectY, AccessType.Read).BaseCycles; }

    // ── ORA ───────────────────────────────────────────────────────────────────
    private void ORA_Imm()  { A |= ReadByte(AddrImmediate());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Immediate, AccessType.Read).BaseCycles; }
    private void ORA_Zp()   { A |= ReadByte(AddrZeroPage());         SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void ORA_ZpX()  { A |= ReadByte(AddrZeroPageX());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Read).BaseCycles; }
    private void ORA_Abs()  { A |= ReadByte(AddrAbsolute());         SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }
    private void ORA_AbsX() { A |= ReadByte(AddrAbsoluteX());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Read).BaseCycles; }
    private void ORA_AbsY() { A |= ReadByte(AddrAbsoluteY());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteY, AccessType.Read).BaseCycles; }
    private void ORA_IndX() { A |= ReadByte(AddrIndexedIndirect());  SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectX, AccessType.Read).BaseCycles; }
    private void ORA_IndY() { A |= ReadByte(AddrIndirectIndexed());  SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectY, AccessType.Read).BaseCycles; }

    // ── EOR ───────────────────────────────────────────────────────────────────
    private void EOR_Imm()  { A ^= ReadByte(AddrImmediate());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Immediate, AccessType.Read).BaseCycles; }
    private void EOR_Zp()   { A ^= ReadByte(AddrZeroPage());         SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void EOR_ZpX()  { A ^= ReadByte(AddrZeroPageX());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Read).BaseCycles; }
    private void EOR_Abs()  { A ^= ReadByte(AddrAbsolute());         SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }
    private void EOR_AbsX() { A ^= ReadByte(AddrAbsoluteX());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Read).BaseCycles; }
    private void EOR_AbsY() { A ^= ReadByte(AddrAbsoluteY());        SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteY, AccessType.Read).BaseCycles; }
    private void EOR_IndX() { A ^= ReadByte(AddrIndexedIndirect());  SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectX, AccessType.Read).BaseCycles; }
    private void EOR_IndY() { A ^= ReadByte(AddrIndirectIndexed());  SetZN(A); TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectY, AccessType.Read).BaseCycles; }

    // ── BIT ───────────────────────────────────────────────────────────────────
    private void BIT_Zp()  { DoBIT(ReadByte(AddrZeroPage()));  TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void BIT_Abs() { DoBIT(ReadByte(AddrAbsolute())); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }

    private void DoBIT(byte val)
    {
        Z = (A & val) == 0;
        N = (val & 0x80) != 0;
        V = (val & 0x40) != 0;
    }
}
