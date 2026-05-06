namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // Illegal helpers that are not in the addressing mode file
    private ushort IllIndX() => AddrIndexedIndirect();
    private ushort IllIndY() => AddrIndirectIndexed(alwaysAddCycle: true);

    // ── Compound illegal operations ───────────────────────────────────────────

    private void DcpAt(ushort addr)
    {
        byte v = (byte)(ReadByte(addr) - 1);
        WriteByte(addr, v);
        int r = A - v;
        C = r >= 0; Z = (r & 0xFF) == 0; N = (r & CpuConstants.BIT_7_MASK) != 0;
    }

    private void IsbAt(ushort addr)
    {
        byte v = (byte)(ReadByte(addr) + 1);
        WriteByte(addr, v);
        SbcCore(v);
    }

    private void SloAt(ushort addr)
    {
        byte v = ReadByte(addr);
        C = (v & CpuConstants.BIT_7_MASK) != 0;
        v <<= 1;
        WriteByte(addr, v);
        A |= v; SetZN(A);
    }

    private void RlaAt(ushort addr)
    {
        byte v = ReadByte(addr);
        byte old = v;
        v = (byte)((v << 1) | (C ? 1 : 0));
        C = (old & CpuConstants.BIT_7_MASK) != 0;
        WriteByte(addr, v);
        A &= v; SetZN(A);
    }

    private void SreAt(ushort addr)
    {
        byte v = ReadByte(addr);
        C = (v & CpuConstants.BIT_0_MASK) != 0;
        v >>= 1;
        WriteByte(addr, v);
        A ^= v; SetZN(A);
    }

    private void RraAt(ushort addr)
    {
        byte v = ReadByte(addr);
        byte old = v;
        v = (byte)((v >> 1) | (C ? CpuConstants.BIT_7_MASK : 0));
        C = (old & CpuConstants.BIT_0_MASK) != 0;
        WriteByte(addr, v);
        AdcCore(v);
    }
}
