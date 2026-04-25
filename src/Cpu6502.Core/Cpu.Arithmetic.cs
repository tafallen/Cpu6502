namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── ADC ───────────────────────────────────────────────────────────────────
    private void ADC_Imm()  { DoADC(ReadByte(AddrImmediate()));                       TotalCycles += 2; }
    private void ADC_Zp()   { DoADC(ReadByte(AddrZeroPage()));                        TotalCycles += 3; }
    private void ADC_ZpX()  { DoADC(ReadByte(AddrZeroPageX()));                       TotalCycles += 4; }
    private void ADC_Abs()  { DoADC(ReadByte(AddrAbsolute()));                        TotalCycles += 4; }
    private void ADC_AbsX() { DoADC(ReadByte(AddrAbsoluteX()));                       TotalCycles += 4; }
    private void ADC_AbsY() { DoADC(ReadByte(AddrAbsoluteY()));                       TotalCycles += 4; }
    private void ADC_IndX() { DoADC(ReadByte(AddrIndexedIndirect()));                 TotalCycles += 6; }
    private void ADC_IndY() { DoADC(ReadByte(AddrIndirectIndexed()));                 TotalCycles += 5; }

    private void DoADC(byte val)
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
    private void SBC_Imm()  { DoSBC(ReadByte(AddrImmediate()));                       TotalCycles += 2; }
    private void SBC_Zp()   { DoSBC(ReadByte(AddrZeroPage()));                        TotalCycles += 3; }
    private void SBC_ZpX()  { DoSBC(ReadByte(AddrZeroPageX()));                       TotalCycles += 4; }
    private void SBC_Abs()  { DoSBC(ReadByte(AddrAbsolute()));                        TotalCycles += 4; }
    private void SBC_AbsX() { DoSBC(ReadByte(AddrAbsoluteX()));                       TotalCycles += 4; }
    private void SBC_AbsY() { DoSBC(ReadByte(AddrAbsoluteY()));                       TotalCycles += 4; }
    private void SBC_IndX() { DoSBC(ReadByte(AddrIndexedIndirect()));                 TotalCycles += 6; }
    private void SBC_IndY() { DoSBC(ReadByte(AddrIndirectIndexed()));                 TotalCycles += 5; }

    private void DoSBC(byte val)
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
            DoADC((byte)~val);
        }
    }

    // ── INC / DEC (memory) ────────────────────────────────────────────────────
    private void INC_Zp()   { var a = AddrZeroPage();              RMW(a, v => ++v); TotalCycles += 5; }
    private void INC_ZpX()  { var a = AddrZeroPageX();             RMW(a, v => ++v); TotalCycles += 6; }
    private void INC_Abs()  { var a = AddrAbsolute();              RMW(a, v => ++v); TotalCycles += 6; }
    private void INC_AbsX() { var a = AddrAbsoluteX(); RMW(a, v => ++v); TotalCycles += 7; }

    private void DEC_Zp()   { var a = AddrZeroPage();              RMW(a, v => --v); TotalCycles += 5; }
    private void DEC_ZpX()  { var a = AddrZeroPageX();             RMW(a, v => --v); TotalCycles += 6; }
    private void DEC_Abs()  { var a = AddrAbsolute();              RMW(a, v => --v); TotalCycles += 6; }
    private void DEC_AbsX() { var a = AddrAbsoluteX(); RMW(a, v => --v); TotalCycles += 7; }

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
