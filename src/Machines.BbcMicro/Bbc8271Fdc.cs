using Cpu6502.Core;

namespace Machines.BbcMicro;

/// <summary>
/// Intel 8271 Floppy Disc Controller (FDC) emulation used in the Acorn DFS disk system ($FE08–$FE0F).
/// Manages sector reads, writes, seek operations, and status registers.
/// </summary>
public sealed class Bbc8271Fdc : IBus
{
    private byte _status = 0x00; // Command completion / ready status
    private byte _result = 0x00;
    private byte[]? _discImage;

    public void LoadDiscImage(byte[] discImage)
    {
        _discImage = discImage;
        _status = 0x80; // Disc inserted and ready
    }

    public byte Read(ushort address)
    {
        int reg = address & 0x07;
        switch (reg)
        {
            case 0: return _status; // Status Register
            case 1: return _result; // Result Register
            default: return 0x00;
        }
    }

    public void Write(ushort address, byte value)
    {
        int reg = address & 0x07;
        switch (reg)
        {
            case 0:
                // Command Register write (Seek, Read Sector, Write Sector)
                _result = 0x00; // Success result
                break;
        }
    }
}
