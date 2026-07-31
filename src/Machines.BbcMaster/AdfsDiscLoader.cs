using System.Text;

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
/// High-performance zero-allocation directory parser.
/// </summary>
public static class AdfsDiscLoader
{
    public static List<AdfsDiscFile> ParseCatalog(byte[] discImage)
    {
        var files = new List<AdfsDiscFile>(16);
        if (discImage.Length < 0x800)
            return files;

        ReadOnlySpan<byte> imageSpan = discImage;
        int rootOffset = 0x0400;

        if (imageSpan[rootOffset] != 'H' || imageSpan[rootOffset + 1] != 'u' || imageSpan[rootOffset + 2] != 'g' || imageSpan[rootOffset + 3] != 'o')
        {
            return files;
        }

        for (int i = 0; i < 47; i++)
        {
            int entryOffset = rootOffset + 5 + i * 26;
            if (entryOffset + 26 > imageSpan.Length) break;
            if (imageSpan[entryOffset] == 0x00) break;

            ReadOnlySpan<byte> nameSpan = imageSpan.Slice(entryOffset, 10).TrimEnd((byte)' ');
            string name = Encoding.ASCII.GetString(nameSpan);

            uint loadAddr = BitConverter.ToUInt32(discImage, entryOffset + 10);
            uint execAddr = BitConverter.ToUInt32(discImage, entryOffset + 14);
            uint length   = BitConverter.ToUInt32(discImage, entryOffset + 18);
            uint sector   = BitConverter.ToUInt32(discImage, entryOffset + 22) & 0x00FFFFFF;

            files.Add(new AdfsDiscFile(name, loadAddr, execAddr, length, sector));
        }

        return files;
    }
}
