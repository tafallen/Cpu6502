using Cpu6502.Core;
using Xunit;

namespace Cpu6502.Tests;

public class AddressDecoderTests
{
    [Fact]
    public void Routes_Read_ToCorrectDevice()
    {
        var ram = new Ram(0x8000);
        ram.Write(0x0100, 0x42);

        var rom = new Rom(new byte[0x8000]);

        var bus = new AddressDecoder();
        bus.Map(0x0000, 0x7FFF, ram);
        bus.Map(0x8000, 0xFFFF, rom);

        Assert.Equal(0x42, bus.Read(0x0100));
    }

    [Fact]
    public void Routes_Write_ToCorrectDevice()
    {
        var ram = new Ram(0x8000);
        var bus = new AddressDecoder();
        bus.Map(0x0000, 0x7FFF, ram);

        bus.Write(0x0200, 0x99);
        Assert.Equal(0x99, ram.Read(0x0200));
    }

    [Fact]
    public void UnmappedRead_Returns0xFF()
    {
        var bus = new AddressDecoder();
        Assert.Equal(0xFF, bus.Read(0x1000));
    }

    [Fact]
    public void UnmappedWrite_IsSilent()
    {
        var bus = new AddressDecoder();
        var ex = Record.Exception(() => bus.Write(0x1000, 0xAB));
        Assert.Null(ex);
    }

    [Fact]
    public void LastMapping_WinsOnOverlap()
    {
        var ram1 = new Ram(0x10000);
        var ram2 = new Ram(0x10000);
        ram1.Write(0x0000, 0x11);
        ram2.Write(0x0000, 0x22);

        var bus = new AddressDecoder();
        bus.Map(0x0000, 0xFFFF, ram1);
        bus.Map(0x0000, 0xFFFF, ram2);  // overrides ram1

        Assert.Equal(0x22, bus.Read(0x0000));
    }

    [Fact]
    public void Boundary_Addresses_Are_Inclusive()
    {
        var ram = new Ram(0x10000);
        ram.Write(0x0000, 0xAA);
        ram.Write(0x00FF, 0xBB);

        var bus = new AddressDecoder();
        bus.Map(0x0000, 0x00FF, ram);

        Assert.Equal(0xAA, bus.Read(0x0000));
        Assert.Equal(0xBB, bus.Read(0x00FF));
    }
}
