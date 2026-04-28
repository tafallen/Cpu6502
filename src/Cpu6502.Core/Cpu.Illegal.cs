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
        C = r >= 0; Z = (r & 0xFF) == 0; N = (r & 0x80) != 0;
    }

    private void IsbAt(ushort addr)
    {
        byte v = (byte)(ReadByte(addr) + 1);
        WriteByte(addr, v);
        DoSbc(v);
    }

    private void SloAt(ushort addr)
    {
        byte v = ReadByte(addr);
        C = (v & 0x80) != 0;
        v <<= 1;
        WriteByte(addr, v);
        A |= v; SetZN(A);
    }

    private void RlaAt(ushort addr)
    {
        byte v = ReadByte(addr);
        byte old = v;
        v = (byte)((v << 1) | (C ? 1 : 0));
        C = (old & 0x80) != 0;
        WriteByte(addr, v);
        A &= v; SetZN(A);
    }

    private void SreAt(ushort addr)
    {
        byte v = ReadByte(addr);
        C = (v & 0x01) != 0;
        v >>= 1;
        WriteByte(addr, v);
        A ^= v; SetZN(A);
    }

    private void RraAt(ushort addr)
    {
        byte v = ReadByte(addr);
        byte old = v;
        v = (byte)((v >> 1) | (C ? 0x80 : 0));
        C = (old & 0x01) != 0;
        WriteByte(addr, v);
        DoAdc(v);
    }

    // Shared ADC/SBC logic reused by RRA/ISB
    private void DoAdc(byte v)
    {
        if (D)
        {
            int lo = (A & 0x0F) + (v & 0x0F) + (C ? 1 : 0);
            if (lo > 9) lo += 6;
            int hi = (A >> 4) + (v >> 4) + (lo > 0x0F ? 1 : 0);
            Z = ((A + v + (C ? 1 : 0)) & 0xFF) == 0;
            N = (hi & 0x08) != 0;
            V = ((~(A ^ v) & (A ^ (hi << 4)) & 0x80)) != 0;
            if (hi > 9) hi += 6;
            C = hi > 0x0F;
            A = (byte)((hi << 4) | (lo & 0x0F));
        }
        else
        {
            int r = A + v + (C ? 1 : 0);
            V = ((~(A ^ v) & (A ^ r) & 0x80)) != 0;
            C = r > 0xFF;
            A = (byte)r;
            SetZN(A);
        }
    }

    private void DoSbc(byte v)
    {
        DoAdc((byte)~v);
    }
}
