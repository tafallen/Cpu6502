using Cpu6502.Core;

namespace Machines.BbcMicro;

/// <summary>
/// 16 KB OS ROM device for the BBC Micro Model B ($C000–$FFFF).
/// Handles address offset subtraction ($C000) so that $C000 maps to offset 0x0000
/// and $FFFC (RESET vector) maps to offset 0x3FFC.
/// </summary>
public sealed class BbcOsRom : IBus
{
    private readonly byte[] _data;

    public BbcOsRom(byte[] data)
    {
        _data = new byte[0x4000];
        Array.Fill(_data, (byte)0xFF);
        int bytesToCopy = Math.Min(data.Length, 0x4000);
        Array.Copy(data, 0, _data, 0, bytesToCopy);
    }

    public byte Read(ushort address)
    {
        if (address < 0x4000)
            return _data[address];
        return 0xFF;
    }

    public void Write(ushort address, byte value)
    {
        // OS ROM ignores CPU writes
    }
}
