using Cpu6502.Core;
using Xunit;

namespace Cpu6502.Tests;

public class AddressDecoderTests
{
    private sealed class SpyBus : IBus
    {
        public ushort LastReadAddress { get; private set; }
        public ushort LastWriteAddress { get; private set; }
        public byte ReadValue { get; set; }

        public byte Read(ushort address)
        {
            LastReadAddress = address;
            return ReadValue;
        }

        public void Write(ushort address, byte value)
        {
            LastWriteAddress = address;
        }
    }

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
    public void LastMapping_WinsOnlyInside_OverlappedWindow()
    {
        var baseRam = new Ram(0x10000);
        var overlay = new Ram(0x0100);

        baseRam.Write(0x0010, 0x11);
        baseRam.Write(0x0011, 0x22);
        overlay.Write(0x0000, 0xAA);

        var bus = new AddressDecoder();
        bus.Map(0x0000, 0xFFFF, baseRam);
        bus.Map(0x0010, 0x0010, overlay);

        Assert.Equal(0xAA, bus.Read(0x0010)); // overlaid
        Assert.Equal(0x22, bus.Read(0x0011)); // falls back to base mapping
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

    [Fact]
    public void Maps_Addresses_As_Offsets_FromRangeStart()
    {
        var spy = new SpyBus { ReadValue = 0x5A };
        var bus = new AddressDecoder();
        bus.Map(0x4000, 0x40FF, spy);

        Assert.Equal(0x5A, bus.Read(0x4080));
        Assert.Equal(0x0080, spy.LastReadAddress);

        bus.Write(0x40FE, 0x99);
        Assert.Equal(0x00FE, spy.LastWriteAddress);
    }

    [Fact]
    public void Map_Throws_When_FromIsGreaterThanTo()
    {
        var bus = new AddressDecoder();
        var ram = new Ram(0x100);

        Assert.Throws<ArgumentException>(() => bus.Map(0x4000, 0x3FFF, ram));
    }

    [Fact]
    public void SingleAddressRange_UsesZeroOffset()
    {
        var spy = new SpyBus { ReadValue = 0xA5 };
        var bus = new AddressDecoder();
        bus.Map(0x1234, 0x1234, spy);

        Assert.Equal(0xA5, bus.Read(0x1234));
        Assert.Equal(0x0000, spy.LastReadAddress);

        bus.Write(0x1234, 0x55);
        Assert.Equal(0x0000, spy.LastWriteAddress);
    }
}
