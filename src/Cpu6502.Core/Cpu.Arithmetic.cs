namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── ADC ───────────────────────────────────────────────────────────────────
    private void ADC_Imm()  { AdcCore(ReadByte(AddrImmediate()));                     TotalCycles += (ulong)GetCycleInfo(AddressingMode.Immediate, AccessType.Read).BaseCycles; }
    private void ADC_Zp()   { AdcCore(ReadByte(AddrZeroPage()));                      TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void ADC_ZpX()  { AdcCore(ReadByte(AddrZeroPageX()));                     TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Read).BaseCycles; }
    private void ADC_Abs()  { AdcCore(ReadByte(AddrAbsolute()));                      TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }
    private void ADC_AbsX() { AdcCore(ReadByte(AddrAbsoluteX()));                     TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Read).BaseCycles; }
    private void ADC_AbsY() { AdcCore(ReadByte(AddrAbsoluteY()));                     TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteY, AccessType.Read).BaseCycles; }
    private void ADC_IndX() { AdcCore(ReadByte(AddrIndexedIndirect()));               TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectX, AccessType.Read).BaseCycles; }
    private void ADC_IndY() { AdcCore(ReadByte(AddrIndirectIndexed()));               TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectY, AccessType.Read).BaseCycles; }

    private void AdcCore(byte val)
    {
        if (D)
        {
            // BCD mode
            int lo = (A & 0x0F) + (val & 0x0F) + (C ? 1 : 0);
            if (lo > 9) lo += 6;
            int hi = (A >> 4) + (val >> 4) + (lo > 15 ? 1 : 0);
            if (hi > 9) hi += 6;
            int result = (hi << 4) | (lo & 0x0F);
            C = hi > 15;
            A = (byte)(result & 0xFF);
            Z = A == 0;
            N = (A & 0x80) != 0;
            // Overflow is undefined in BCD on NMOS 6502; we don't set it
        }
        else
        {
            int result = A + val + (C ? 1 : 0);
            V = ((A ^ result) & (val ^ result) & 0x80) != 0;
            C = result > 0xFF;
            A = (byte)(result & 0xFF);
            SetZN(A);
        }
    }

    // ── SBC ───────────────────────────────────────────────────────────────────
    private void SBC_Imm()  { SbcCore(ReadByte(AddrImmediate()));                     TotalCycles += (ulong)GetCycleInfo(AddressingMode.Immediate, AccessType.Read).BaseCycles; }
    private void SBC_Zp()   { SbcCore(ReadByte(AddrZeroPage()));                      TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Read).BaseCycles; }
    private void SBC_ZpX()  { SbcCore(ReadByte(AddrZeroPageX()));                     TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Read).BaseCycles; }
    private void SBC_Abs()  { SbcCore(ReadByte(AddrAbsolute()));                      TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Read).BaseCycles; }
    private void SBC_AbsX() { SbcCore(ReadByte(AddrAbsoluteX()));                     TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Read).BaseCycles; }
    private void SBC_AbsY() { SbcCore(ReadByte(AddrAbsoluteY()));                     TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteY, AccessType.Read).BaseCycles; }
    private void SBC_IndX() { SbcCore(ReadByte(AddrIndexedIndirect()));               TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectX, AccessType.Read).BaseCycles; }
    private void SBC_IndY() { SbcCore(ReadByte(AddrIndirectIndexed()));               TotalCycles += (ulong)GetCycleInfo(AddressingMode.IndirectY, AccessType.Read).BaseCycles; }

    private void SbcCore(byte val)
    {
        if (D)
        {
            // BCD mode
            int lo = (A & 0x0F) - (val & 0x0F) - (C ? 0 : 1);
            if (lo < 0) lo -= 6;
            int hi = (A >> 4) - (val >> 4) - (lo < 0 ? 1 : 0);
            if (hi < 0) hi -= 6;
            int result = (hi << 4) | (lo & 0x0F);
            C = hi >= 0;
            A = (byte)(result & 0xFF);
            Z = A == 0;
            N = (A & 0x80) != 0;
        }
        else
        {
            // SBC is ADC with the operand inverted
            AdcCore((byte)~val);
        }
    }

    // ── INC / DEC (memory) ────────────────────────────────────────────────────
    private void INC_Zp()   { var a = AddrZeroPage();              RMW(a, v => ++v); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Rmw).BaseCycles; }
    private void INC_ZpX()  { var a = AddrZeroPageX();             RMW(a, v => ++v); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Rmw).BaseCycles; }
    private void INC_Abs()  { var a = AddrAbsolute();              RMW(a, v => ++v); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Rmw).BaseCycles; }
    private void INC_AbsX() { var a = AddrAbsoluteX(); RMW(a, v => ++v); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Rmw).BaseCycles; }

    private void DEC_Zp()   { var a = AddrZeroPage();              RMW(a, v => --v); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Rmw).BaseCycles; }
    private void DEC_ZpX()  { var a = AddrZeroPageX();             RMW(a, v => --v); TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Rmw).BaseCycles; }
    private void DEC_Abs()  { var a = AddrAbsolute();              RMW(a, v => --v); TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Rmw).BaseCycles; }
    private void DEC_AbsX() { var a = AddrAbsoluteX(); RMW(a, v => --v); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Rmw).BaseCycles; }

    private void RMW(ushort address, Func<byte, byte> op)
    {
        byte result = op(ReadByte(address));
        WriteByte(address, result);
        SetZN(result);
    }

    // ── Register inc/dec ──────────────────────────────────────────────────────
    private void INX() { X++; SetZN(X); TotalCycles += 2; }
    private void INY() { Y++; SetZN(Y); TotalCycles += 2; }
    private void DEX() { X--; SetZN(X); TotalCycles += 2; }
    private void DEY() { Y--; SetZN(Y); TotalCycles += 2; }
}
