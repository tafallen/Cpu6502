using Machines.BbcMaster;
using Xunit;

namespace Machines.BbcMaster.Tests;

public class AdfsCatalogTests
{
    [Fact]
    public void AdfsDiscLoader_ParsesHugoDirectoryHeader()
    {
        byte[] discImage = new byte[0x800];
        int rootOffset = 0x0400;

        // Write "Hugo" root identifier
        discImage[rootOffset]     = (byte)'H';
        discImage[rootOffset + 1] = (byte)'u';
        discImage[rootOffset + 2] = (byte)'g';
        discImage[rootOffset + 3] = (byte)'o';

        // Write first entry: "GAME      "
        int entryOffset = rootOffset + 5;
        byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes("GAME      ");
        Array.Copy(nameBytes, 0, discImage, entryOffset, 10);

        var files = AdfsDiscLoader.ParseCatalog(discImage);

        Assert.Single(files);
        Assert.Equal("GAME", files[0].Name);
    }
}
