using Machines.BbcMicro;
using Xunit;

namespace Machines.BbcMicro.Tests;

public class DfsCatalogTests
{
    [Fact]
    public void ParseCatalog_SingleSidedSsd_ParsesFileMetadataCorrectly()
    {
        // 200 KB SSD disc image (80 tracks * 10 sectors * 256 bytes)
        byte[] ssdData = new byte[80 * 10 * 256];

        // Set catalog sector 1 file count to 2 files (2 * 8 = 16 bytes)
        ssdData[0x0105] = 16;

        // File 1: "WELCOME" in dir "$"
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("WELCOME$"), 0, ssdData, 0x0000, 8);
        ssdData[0x0108] = 0x00; // Load address low $0E00
        ssdData[0x0109] = 0x0E; // Load address high
        ssdData[0x010A] = 0x00; // Exec address low $0E00
        ssdData[0x010B] = 0x0E; // Exec address high
        ssdData[0x010C] = 0x00; // Length low $0400 (1024 bytes)
        ssdData[0x010D] = 0x04; // Length high
        ssdData[0x010F] = 0x02; // Start sector 2

        // File 2: "GAME   $"
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("GAME   $"), 0, ssdData, 0x0008, 8);
        ssdData[0x0110] = 0x00; // Load address low $1900
        ssdData[0x0111] = 0x19; // Load address high
        ssdData[0x0112] = 0x00; // Exec address low $1900
        ssdData[0x0113] = 0x19; // Exec address high
        ssdData[0x0114] = 0x00; // Length low $1000 (4096 bytes)
        ssdData[0x0115] = 0x10; // Length high
        ssdData[0x0117] = 0x06; // Start sector 6

        var files = DfsDiscLoader.ParseCatalog(ssdData);

        Assert.Equal(2, files.Count);

        Assert.Equal("WELCOME", files[0].Filename);
        Assert.Equal(0x0E00, files[0].LoadAddress);
        Assert.Equal(0x0E00, files[0].ExecutionAddress);
        Assert.Equal(0x0400, files[0].Length);
        Assert.Equal(2, files[0].SectorOffset);

        Assert.Equal("GAME", files[1].Filename);
        Assert.Equal(0x1900, files[1].LoadAddress);
        Assert.Equal(0x1900, files[1].ExecutionAddress);
        Assert.Equal(0x1000, files[1].Length);
        Assert.Equal(6, files[1].SectorOffset);
    }
}
