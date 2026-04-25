using Cpu6502.Core;
using Xunit;

namespace Cpu6502.Tests;

public class MemoryTests
{
    // ── Ram ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Ram_ReadsBackWrittenByte()
    {
        var ram = new Ram(0x10000);
        ram.Write(0x0200, 0xAB);
        Assert.Equal(0xAB, ram.Read(0x0200));
    }

    [Fact]
    public void Ram_InitialisesToZero()
    {
        var ram = new Ram(0x10000);
        Assert.Equal(0x00, ram.Read(0x1234));
    }

    [Fact]
    public void Ram_Load_PlacesBytesAtOffset()
    {
        var ram = new Ram(0x10000);
        ram.Load(0x0300, new byte[] { 0x01, 0x02, 0x03 });
        Assert.Equal(0x01, ram.Read(0x0300));
        Assert.Equal(0x02, ram.Read(0x0301));
        Assert.Equal(0x03, ram.Read(0x0302));
    }

    [Fact]
    public void Ram_Load_DoesNotOverwriteOtherRegions()
    {
        var ram = new Ram(0x10000);
        ram.Write(0x02FF, 0xFF);
        ram.Load(0x0300, new byte[] { 0xAA });
        Assert.Equal(0xFF, ram.Read(0x02FF));
    }

    // ── Rom ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Rom_ReadsCorrectByte()
    {
        var rom = new Rom(new byte[] { 0x10, 0x20, 0x30 });
        Assert.Equal(0x20, rom.Read(0x0001));
    }

    [Fact]
    public void Rom_IgnoresWrites()
    {
        var rom = new Rom(new byte[] { 0xAA, 0xBB });
        rom.Write(0x0000, 0xFF);           // should have no effect
        Assert.Equal(0xAA, rom.Read(0x0000));
    }
}
