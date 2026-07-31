using Cpu6502.Core;

namespace Machines.Pet;

/// <summary>
/// 28 KB ROM region for Commodore PET 2001 / 4032 / 8032 ($9000–$FFFF).
/// Contains BASIC ROMs ($9000–$BFFF), Editor ROM ($E000–$E7FF),
/// Kernel ROM ($F000–$FFFF), and MMIO window at $E800–$EFFF.
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
        int offset = address - 0x9000;
        if (offset >= 0 && offset < 0x7000)
            return _data[offset];
        return 0xFF;
    }

    public void Write(ushort address, byte value)
    {
        // PET ROM ignores writes
    }
}
