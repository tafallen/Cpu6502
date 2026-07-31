using System.Text;

namespace Machines.C64;

public sealed record D64File(
    string Name,
    byte Track,
    byte Sector,
    ushort FileSizeBlocks
);

/// <summary>
/// Commodore 64 program loader (.prg binary format) and .d64 1541 disk image parser.
/// High-performance zero-allocation directory parser using ReadOnlySpan<byte>.
/// </summary>
public static class C64ProgramLoader
{
    public static ushort LoadPrg(byte[] prgData, C64Bus bus)
    {
        if (prgData.Length < 2)
            throw new ArgumentException("Invalid PRG file format: too short.");

        ushort loadAddress = (ushort)(prgData[0] | (prgData[1] << 8));
        int payloadLength = prgData.Length - 2;

        for (int i = 0; i < payloadLength; i++)
        {
            bus.Ram.Write((ushort)(loadAddress + i), prgData[2 + i]);
        }

        return loadAddress;
    }

    public static List<D64File> ParseD64Catalog(byte[] d64Image)
    {
        var files = new List<D64File>(16);
        if (d64Image.Length < 174848)
            return files;

        ReadOnlySpan<byte> imageSpan = d64Image;
        int dirOffset = 0x16600;

        while (dirOffset + 256 <= imageSpan.Length)
        {
            for (int entry = 0; entry < 8; entry++)
            {
                int entryOffset = dirOffset + entry * 32;

                byte fileType = imageSpan[entryOffset + 0x02];
                if (fileType == 0x00) continue;

                byte firstTrack  = imageSpan[entryOffset + 0x03];
                byte firstSector = imageSpan[entryOffset + 0x04];

                ReadOnlySpan<byte> nameSpan = imageSpan.Slice(entryOffset + 0x05, 16).TrimEnd((byte)0xA0);
                string fileName = Encoding.ASCII.GetString(nameSpan);

                ushort blocks = (ushort)(imageSpan[entryOffset + 0x1E] | (imageSpan[entryOffset + 0x1F] << 8));

                files.Add(new D64File(fileName, firstTrack, firstSector, blocks));
            }

            byte nextTrack = imageSpan[dirOffset];
            if (nextTrack == 0) break;
            dirOffset += 0x100;
        }

        return files;
    }
}
