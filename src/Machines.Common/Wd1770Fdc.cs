using Cpu6502.Core;

namespace Machines.Common;

/// <summary>
/// Western Digital 1770 / 1772 Floppy Disk Controller.
/// Reused across BBC Master, Atari ST, and Commodore 1581 disk drives.
/// </summary>
public class Wd1770Fdc : IBus
{
    private readonly byte[] _registers = new byte[4];

    public byte CommandStatus
    {
        get => _registers[0];
        set => _registers[0] = value;
    }

    public byte Track
    {
        get => _registers[1];
        set => _registers[1] = value;
    }

    public byte Sector
    {
        get => _registers[2];
        set => _registers[2] = value;
    }

    public byte Data
    {
        get => _registers[3];
        set => _registers[3] = value;
    }

    public byte Read(ushort address)
    {
        return _registers[address & 3];
    }

    public void Write(ushort address, byte value)
    {
        byte reg = (byte)(address & 3);
        _registers[reg] = value;

        if (reg == 0) // Command register write
        {
            if ((value & 0xF0) == 0x10) // Seek command
            {
                _registers[1] = _registers[3]; // Track = Data
            }
        }
    }
}
