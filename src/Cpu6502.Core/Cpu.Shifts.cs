namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── ASL ───────────────────────────────────────────────────────────────────
    private void ASL_Acc()  { A = DoASL(A);                                      TotalCycles += 2; }
    private void ASL_Zp()   { var a = AddrZeroPage();  DoASL_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Rmw).BaseCycles; }
    private void ASL_ZpX()  { var a = AddrZeroPageX(); DoASL_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Rmw).BaseCycles; }
    private void ASL_Abs()  { var a = AddrAbsolute();  DoASL_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Rmw).BaseCycles; }
    private void ASL_AbsX() { var a = AddrAbsoluteX(); DoASL_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Rmw).BaseCycles; }

    private byte DoASL(byte val) { C = (val & 0x80) != 0; val <<= 1; SetZN(val); return val; }

    private void DoASL_Mem(ushort address)
    {
        byte val = ReadByte(address);
        C = (val & 0x80) != 0;
        val <<= 1;
        WriteByte(address, val);
        SetZN(val);
    }

    // ── LSR ───────────────────────────────────────────────────────────────────
    private void LSR_Acc()  { A = DoLSR(A);                                      TotalCycles += 2; }
    private void LSR_Zp()   { var a = AddrZeroPage();  DoLSR_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Rmw).BaseCycles; }
    private void LSR_ZpX()  { var a = AddrZeroPageX(); DoLSR_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Rmw).BaseCycles; }
    private void LSR_Abs()  { var a = AddrAbsolute();  DoLSR_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Rmw).BaseCycles; }
    private void LSR_AbsX() { var a = AddrAbsoluteX(); DoLSR_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Rmw).BaseCycles; }

    private byte DoLSR(byte val) { C = (val & 0x01) != 0; val >>= 1; SetZN(val); return val; }

    private void DoLSR_Mem(ushort address)
    {
        byte val = ReadByte(address);
        C = (val & 0x01) != 0;
        val >>= 1;
        WriteByte(address, val);
        SetZN(val);
    }

    // ── ROL ───────────────────────────────────────────────────────────────────
    private void ROL_Acc()  { A = DoROL(A);                                      TotalCycles += 2; }
    private void ROL_Zp()   { var a = AddrZeroPage();  DoROL_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Rmw).BaseCycles; }
    private void ROL_ZpX()  { var a = AddrZeroPageX(); DoROL_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Rmw).BaseCycles; }
    private void ROL_Abs()  { var a = AddrAbsolute();  DoROL_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Rmw).BaseCycles; }
    private void ROL_AbsX() { var a = AddrAbsoluteX(); DoROL_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Rmw).BaseCycles; }

    private byte DoROL(byte val)
    {
        bool oldCarry = C;
        C = (val & 0x80) != 0;
        val = (byte)((val << 1) | (oldCarry ? 1 : 0));
        SetZN(val);
        return val;
    }

    private void DoROL_Mem(ushort address)
    {
        byte val = ReadByte(address);
        bool oldCarry = C;
        C = (val & 0x80) != 0;
        val = (byte)((val << 1) | (oldCarry ? 1 : 0));
        WriteByte(address, val);
        SetZN(val);
    }

    // ── ROR ───────────────────────────────────────────────────────────────────
    private void ROR_Acc()  { A = DoROR(A);                                      TotalCycles += 2; }
    private void ROR_Zp()   { var a = AddrZeroPage();  DoROR_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPage, AccessType.Rmw).BaseCycles; }
    private void ROR_ZpX()  { var a = AddrZeroPageX(); DoROR_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.ZeroPageX, AccessType.Rmw).BaseCycles; }
    private void ROR_Abs()  { var a = AddrAbsolute();  DoROR_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.Absolute, AccessType.Rmw).BaseCycles; }
    private void ROR_AbsX() { var a = AddrAbsoluteX(); DoROR_Mem(a);             TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Rmw).BaseCycles; }

    private byte DoROR(byte val)
    {
        bool oldCarry = C;
        C = (val & 0x01) != 0;
        val = (byte)((val >> 1) | (oldCarry ? 0x80 : 0));
        SetZN(val);
        return val;
    }

    private void DoROR_Mem(ushort address)
    {
        byte val = ReadByte(address);
        bool oldCarry = C;
        C = (val & 0x01) != 0;
        val = (byte)((val >> 1) | (oldCarry ? 0x80 : 0));
        WriteByte(address, val);
        SetZN(val);
    }
}
