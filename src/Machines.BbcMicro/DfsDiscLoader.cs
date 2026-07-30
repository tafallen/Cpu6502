namespace Machines.BbcMicro;

public sealed record DfsFileEntry(
    string Filename,
    byte Directory,
    ushort LoadAddress,
    ushort ExecutionAddress,
    ushort Length,
    ushort SectorOffset
);

/// <summary>
/// Parser for Acorn DFS (Disc Filing System) .ssd (single sided) and .dsd (double sided) disc images.
/// DFS disc catalog lives in Sector 0 (filenames) and Sector 1 (metadata & file counts).
/// </summary>
public static class DfsDiscLoader
{
    public static List<DfsFileEntry> ParseCatalog(byte[] discData)
    {
        var files = new List<DfsFileEntry>();
        if (discData.Length < 512) return files;

        // Sector 1 byte 5 contains (number of files * 8)
        int fileCount = (discData[0x0105] & 0xFF) / 8;
        fileCount = Math.Min(fileCount, 31); // DFS supports up to 31 files

        for (int i = 0; i < fileCount; i++)
        {
            int fnOff = i * 8;
            int metaOff = 0x0108 + i * 8;

            string name = System.Text.Encoding.ASCII.GetString(discData, fnOff, 7).Trim();
            byte dir = (byte)(discData[fnOff + 7] & 0x7F);

            ushort loadAddr = (ushort)(discData[metaOff] | (discData[metaOff + 1] << 8));
            ushort execAddr = (ushort)(discData[metaOff + 2] | (discData[metaOff + 3] << 8));
            ushort length   = (ushort)(discData[metaOff + 4] | (discData[metaOff + 5] << 8));

            ushort startSector = (ushort)(discData[metaOff + 7] | ((discData[metaOff + 6] & 0x03) << 8));

            files.Add(new DfsFileEntry(name, dir, loadAddr, execAddr, length, startSector));
        }

        return files;
    }
}
