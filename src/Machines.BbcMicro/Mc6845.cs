using Cpu6502.Core;

namespace Machines.BbcMicro;

/// <summary>
/// Motorola 6845 CRT Controller (CRTC) used in the BBC Micro Model B ($FE00–$FE07).
/// Manages screen timing, cursor position, and video memory address generation.
/// </summary>
public sealed class Mc6845 : IBus
{
    private byte _addressRegister;
    private readonly byte[] _registers = new byte[32];

    public byte AddressRegister => _addressRegister;

    /// <summary>Screen display start address in RAM (R12 high, R13 low).</summary>
    public ushort DisplayStartAddress => (ushort)(((_registers[12] & 0x3F) << 8) | _registers[13]);

    /// <summary>Cursor position address in RAM (R14 high, R15 low).</summary>
    public ushort CursorAddress => (ushort)(((_registers[14] & 0x3F) << 8) | _registers[15]);

    public byte Read(ushort address)
    {
        if ((address & 1) == 1)
        {
            // Data register read (R14/R15 cursor pos, R16/R17 light pen)
            int reg = _addressRegister & 0x1F;
            return _registers[reg];
        }
        return _addressRegister;
    }

    public void Write(ushort address, byte value)
    {
        if ((address & 1) == 0)
        {
            _addressRegister = (byte)(value & 0x1F);
        }
        else
        {
            int reg = _addressRegister & 0x1F;
            _registers[reg] = value;
        }
    }
}
