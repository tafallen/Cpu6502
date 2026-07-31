namespace Machines.BbcMaster;

public sealed record AdfsDiscFile(
    string Name,
    uint LoadAddress,
    uint ExecAddress,
    uint Length,
    uint Sector
);

/// <summary>
/// ADFS (Acorn Disc Filing System) disc image loader (.adf / .adl format).
/// Parses ADFS directory catalog headers (S / M / L disc structures).
/// </summary>
public static class AdfsDiscLoader
{
    public static List<AdfsDiscFile> ParseCatalog(byte[] discImage)
    {
        var files = new List<AdfsDiscFile>();
        if (discImage.Length < 0x800)
            return files;

        // ADFS S-Format root directory is at sector 2 (offset 0x0400)
        int rootOffset = 0x0400;
        if (discImage[rootOffset] != 'H' || discImage[rootOffset + 1] != 'u' || discImage[rootOffset + 2] != 'g' || discImage[rootOffset + 3] != 'o')
        {
            // Fall back or return empty list if not valid Hugo root
            return files;
        }

        // Parse up to 47 file entries in Hugo root directory
        for (int i = 0; i < 47; i++)
        {
            int entryOffset = rootOffset + 5 + i * 26;
            if (entryOffset + 26 > discImage.Length) break;

            if (discImage[entryOffset] == 0x00) break; // End of catalog entries

            string name = System.Text.Encoding.ASCII.GetString(discImage, entryOffset, 10).TrimEnd();
            uint loadAddr = BitConverter.ToUInt32(discImage, entryOffset + 10);
            uint execAddr = BitConverter.ToUInt32(discImage, entryOffset + 14);
            uint length   = BitConverter.ToUInt32(discImage, entryOffset + 18);
            uint sector   = BitConverter.ToUInt32(discImage, entryOffset + 22) & 0x00FFFFFF;

            files.Add(new AdfsDiscFile(name, loadAddr, execAddr, length, sector));
        }

        return files;
    }
}
