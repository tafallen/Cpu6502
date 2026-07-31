using Cpu6502.Core;

namespace Machines.BbcMaster;

/// <summary>
/// Western Digital 1770 / 1772 Floppy Disk Controller ($FE80–$FE83).
/// High-performance implementation with contiguous 4-byte register array ($O(1)$ direct access).
/// </summary>
public sealed class Wd1770Fdc : IBus
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
        _registers[address & 3] = value;
    }
}
