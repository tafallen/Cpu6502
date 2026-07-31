using Cpu6502.Core;

namespace Machines.Pet;

/// <summary>
/// 28 KB ROM region for Commodore PET 2001 / 4032 / 8032 ($9000–$FFFF).
/// AddressDecoder passes the 0-indexed relative offset from $9000 (0x0000–0x6FFF).
/// </summary>
public sealed class PetRom : IBus
{
    private readonly byte[] _data;

    public PetRom(byte[] data)
    {
        _data = new byte[0x7000];
        Array.Fill(_data, (byte)0xFF);
        int bytesToCopy = Math.Min(data.Length, 0x7000);
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
        // PET ROM ignores writes
    }
}
