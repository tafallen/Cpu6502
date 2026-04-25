namespace Machines.Common;

/// <summary>
/// Represents a virtual disk drive.
/// Implementations back reads/writes with disk image files (.dsk, .ssd, etc.).
/// </summary>
public interface IDiskDevice
{
    ReadOnlySpan<byte> ReadSector(int track, int sector);
    void WriteSector(int track, int sector, ReadOnlySpan<byte> data);
}
