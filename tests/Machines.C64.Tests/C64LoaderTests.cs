using Machines.C64;
using Xunit;

namespace Machines.C64.Tests;

public class C64LoaderTests
{
    [Fact]
    public void LoadPrg_ValidData_LoadsIntoRamAtAddress()
    {
        var bus = new C64Bus();

        // Header: $0801 (little endian: 0x01, 0x08), Payload: 0xEA (NOP), 0x60 (RTS)
        byte[] prg = new byte[] { 0x01, 0x08, 0xEA, 0x60 };

        ushort targetAddr = C64ProgramLoader.LoadPrg(prg, bus);

        Assert.Equal(0x0801, targetAddr);
        Assert.Equal(0xEA, bus.Ram.Read(0x0801));
        Assert.Equal(0x60, bus.Ram.Read(0x0802));
    }

    [Fact]
    public void ParseD64Catalog_ValidD64_ParsesFileEntries()
    {
        byte[] d64 = new byte[174848];

        // Set up directory entry at Track 18, Sector 1 (0x16600)
        int dirOffset = 0x16600;
        d64[dirOffset + 0x02] = 0x82; // PRG file type
        d64[dirOffset + 0x03] = 18;   // Track
        d64[dirOffset + 0x04] = 2;    // Sector
        d64[dirOffset + 0x05] = (byte)'T';
        d64[dirOffset + 0x06] = (byte)'E';
        d64[dirOffset + 0x07] = (byte)'S';
        d64[dirOffset + 0x08] = (byte)'T';
        for (int i = 9; i < 21; i++) d64[dirOffset + i] = 0xA0; // Padding
        d64[dirOffset + 0x1E] = 5; // Blocks

        var catalog = C64ProgramLoader.ParseD64Catalog(d64);

        Assert.Single(catalog);
        Assert.Equal("TEST", catalog[0].Name);
        Assert.Equal(18, catalog[0].Track);
        Assert.Equal(2, catalog[0].Sector);
        Assert.Equal(5, catalog[0].FileSizeBlocks);
    }
}
