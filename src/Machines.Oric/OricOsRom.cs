using Cpu6502.Core;

namespace Machines.Oric;

/// <summary>
/// 16 KB BASIC / OS ROM device for Oric-1 / Oric Atmos ($C000–$FFFF).
/// AddressDecoder passes the 0-indexed relative offset (0x0000–0x3FFF).
/// </summary>
public sealed class OricOsRom : IBus
{
    private readonly byte[] _data;

    public OricOsRom(byte[] data)
    {
        _data = new byte[0x4000];
        Array.Fill(_data, (byte)0xFF);
        int bytesToCopy = Math.Min(data.Length, 0x4000);
        Array.Copy(data, 0, _data, 0, bytesToCopy);
    }

    public byte Read(ushort address)
    {
        if (address < _data.Length)
            return _data[address];
        return 0xFF;
    }

    public void Write(ushort address, byte value)
    {
        // OS ROM ignores writes
    }
}
