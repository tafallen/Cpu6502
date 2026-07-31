using Cpu6502.Core;

namespace Machines.BbcMaster;

/// <summary>
/// Western Digital 1770 / 1772 Floppy Disk Controller (FDC) used in BBC Master 128 ($FE80–$FE83).
/// Supports ADFS (Acorn Disc Filing System) double-density disc formats (.adf / .adl).
/// Registers:
/// $FE80: Command / Status Register
/// $FE81: Track Register
/// $FE82: Sector Register
/// $FE83: Data Register
/// </summary>
public sealed class Wd1770Fdc : IBus
{
    public byte CommandStatus { get; set; }
    public byte Track { get; set; }
    public byte Sector { get; set; }
    public byte Data { get; set; }

    public byte Read(ushort address)
    {
        switch (address & 3)
        {
            case 0: return CommandStatus;
            case 1: return Track;
            case 2: return Sector;
            case 3: return Data;
            default: return 0xFF;
        }
    }

    public void Write(ushort address, byte value)
    {
        switch (address & 3)
        {
            case 0: CommandStatus = value; break;
            case 1: Track = value; break;
            case 2: Sector = value; break;
            case 3: Data = value; break;
        }
    }
}
