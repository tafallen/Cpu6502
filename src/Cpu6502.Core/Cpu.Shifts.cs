namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── ASL ───────────────────────────────────────────────────────────────────
    private void ASL_Acc()  { A = DoASL(A);                                      TotalCycles += 2; }
    private void ASL_Zp()   { var a = AddrZeroPage();  RMW_Shift(a, DoASL);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Rmw).BaseCycles; }
    private void ASL_ZpX()  { var a = AddrZeroPageX(); RMW_Shift(a, DoASL);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Rmw).BaseCycles; }
    private void ASL_Abs()  { var a = AddrAbsolute();  RMW_Shift(a, DoASL);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Rmw).BaseCycles; }
    private void ASL_AbsX() { var a = AddrAbsoluteX(); RMW_Shift(a, DoASL); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Rmw).BaseCycles; }

    private byte DoASL(byte val) { C = (val & 0x80) != 0; val <<= 1; SetZN(val); return val; }

    // ── LSR ───────────────────────────────────────────────────────────────────
    private void LSR_Acc()  { A = DoLSR(A);                                      TotalCycles += 2; }
    private void LSR_Zp()   { var a = AddrZeroPage();  RMW_Shift(a, DoLSR);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Rmw).BaseCycles; }
    private void LSR_ZpX()  { var a = AddrZeroPageX(); RMW_Shift(a, DoLSR);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Rmw).BaseCycles; }
    private void LSR_Abs()  { var a = AddrAbsolute();  RMW_Shift(a, DoLSR);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Rmw).BaseCycles; }
    private void LSR_AbsX() { var a = AddrAbsoluteX(); RMW_Shift(a, DoLSR); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Rmw).BaseCycles; }

    private byte DoLSR(byte val) { C = (val & 0x01) != 0; val >>= 1; SetZN(val); return val; }

    // ── ROL ───────────────────────────────────────────────────────────────────
    private void ROL_Acc()  { A = DoROL(A);                                      TotalCycles += 2; }
    private void ROL_Zp()   { var a = AddrZeroPage();  RMW_Shift(a, DoROL);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Rmw).BaseCycles; }
    private void ROL_ZpX()  { var a = AddrZeroPageX(); RMW_Shift(a, DoROL);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Rmw).BaseCycles; }
    private void ROL_Abs()  { var a = AddrAbsolute();  RMW_Shift(a, DoROL);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Rmw).BaseCycles; }
    private void ROL_AbsX() { var a = AddrAbsoluteX(); RMW_Shift(a, DoROL); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Rmw).BaseCycles; }

    private byte DoROL(byte val)
    {
        bool oldCarry = C;
        C = (val & 0x80) != 0;
        val = (byte)((val << 1) | (oldCarry ? 1 : 0));
        SetZN(val);
        return val;
    }

    // ── ROR ───────────────────────────────────────────────────────────────────
    private void ROR_Acc()  { A = DoROR(A);                                      TotalCycles += 2; }
    private void ROR_Zp()   { var a = AddrZeroPage();  RMW_Shift(a, DoROR);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Rmw).BaseCycles; }
    private void ROR_ZpX()  { var a = AddrZeroPageX(); RMW_Shift(a, DoROR);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Rmw).BaseCycles; }
    private void ROR_Abs()  { var a = AddrAbsolute();  RMW_Shift(a, DoROR);      TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Rmw).BaseCycles; }
    private void ROR_AbsX() { var a = AddrAbsoluteX(); RMW_Shift(a, DoROR); TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Rmw).BaseCycles; }

    private byte DoROR(byte val)
    {
        bool oldCarry = C;
        C = (val & 0x01) != 0;
        val = (byte)((val >> 1) | (oldCarry ? 0x80 : 0));
        SetZN(val);
        return val;
    }

    private void RMW_Shift(ushort address, Func<byte, byte> op)
    {
        byte result = op(ReadByte(address));
        WriteByte(address, result);
    }
}
