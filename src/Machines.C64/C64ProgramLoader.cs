using Cpu6502.Core;

namespace Machines.C64;

public sealed record D64File(
    string Name,
    byte Track,
    byte Sector,
    ushort FileSizeBlocks
);

/// <summary>
/// Commodore 64 program loader (.prg binary format) and .d64 1541 disk image parser.
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

        // Standard 35-track 1541 .d64 image size is 174,848 bytes
        if (d64Image.Length < 174848)
            return files;

        // Track 18, Sector 1 directory block (Offset: 0x16500 + 0x100 = 0x16600)
        int dirOffset = 0x16600;

        while (dirOffset + 256 <= d64Image.Length)
        {
            for (int entry = 0; entry < 8; entry++)
            {
                int entryOffset = dirOffset + entry * 32;

                byte fileType = d64Image[entryOffset + 0x02];
                if (fileType == 0x00) continue; // Unallocated entry

                byte firstTrack  = d64Image[entryOffset + 0x03];
                byte firstSector = d64Image[entryOffset + 0x04];

                // 16-character PETSCII file name (padded with 0xA0)
                char[] nameChars = new char[16];
                for (int i = 0; i < 16; i++)
                {
                    byte b = d64Image[entryOffset + 0x05 + i];
                    nameChars[i] = b == 0xA0 ? ' ' : (char)b;
                }

                string fileName = new string(nameChars).TrimEnd();
                ushort blocks = (ushort)(d64Image[entryOffset + 0x1E] | (d64Image[entryOffset + 0x1F] << 8));

                files.Add(new D64File(fileName, firstTrack, firstSector, blocks));
            }

            byte nextTrack  = d64Image[dirOffset];
            byte nextSector = d64Image[dirOffset + 1];

            if (nextTrack == 0) break; // End of directory chain
            dirOffset += 0x100;
        }

        return files;
    }
}
