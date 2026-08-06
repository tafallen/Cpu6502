using System;
using Cpu6502.Core;
using Machines.Vic20;
using Xunit;

namespace Machines.Vic20.Tests;

public class Vic20ExpansionTests
{
    private static (byte[] basicRom, byte[] kernalRom) CreateDummyRoms()
    {
        return (new byte[0x2000], new byte[0x2000]);
    }

    [Theory]
    [InlineData(RamExpansion.None, 0x0400, true)]
    [InlineData(RamExpansion.None, 0x2000, false)]
    [InlineData(RamExpansion.Ram3K, 0x0FFF, true)]
    [InlineData(RamExpansion.Ram8K, 0x2000, true)]
    [InlineData(RamExpansion.Ram8K, 0x3FFF, true)]
    [InlineData(RamExpansion.Ram8K, 0x4000, false)]
    [InlineData(RamExpansion.Ram16K, 0x4000, true)]
    [InlineData(RamExpansion.Ram16K, 0x5FFF, true)]
    [InlineData(RamExpansion.Ram16K, 0x6000, false)]
    [InlineData(RamExpansion.Ram24K, 0x6000, true)]
    [InlineData(RamExpansion.Ram24K, 0x7FFF, true)]
    [InlineData(RamExpansion.Ram32K, 0x0400, true)]
    [InlineData(RamExpansion.Ram32K, 0x7FFF, true)]
    public void RamExpansion_MapsCorrectAddressRanges(RamExpansion config, ushort address, bool shouldBeMapped)
    {
        var (basicRom, kernalRom) = CreateDummyRoms();
        var machine = new Vic20Machine(basicRom, kernalRom, ramExpansion: config);

        machine.Bus.Write(address, 0x42);
        byte value = machine.Bus.Read(address);

        if (shouldBeMapped)
        {
            Assert.Equal(0x42, value);
        }
        else
        {
            Assert.Equal(0xFF, value);
        }
    }

    [Fact]
    public void CartridgeRom_8KB_MapsToBlock5()
    {
        var (basicRom, kernalRom) = CreateDummyRoms();
        var cartRom = new byte[0x2000];
        cartRom[0x0000] = 0x12;
        cartRom[0x1FFF] = 0x34;

        var machine = new Vic20Machine(basicRom, kernalRom, cartridgeRom: cartRom);

        Assert.Equal(0x12, machine.Bus.Read(0xA000));
        Assert.Equal(0x34, machine.Bus.Read(0xBFFF));
    }

    [Fact]
    public void CartridgeRom_16KB_MapsToBlock3AndBlock5()
    {
        var (basicRom, kernalRom) = CreateDummyRoms();
        var cartRom = new byte[0x4000];
        cartRom[0x0000] = 0xAB; // Block 3 start ($6000)
        cartRom[0x2000] = 0xCD; // Block 5 start ($A000)

        var machine = new Vic20Machine(basicRom, kernalRom, cartridgeRom: cartRom);

        Assert.Equal(0xAB, machine.Bus.Read(0x6000));
        Assert.Equal(0xCD, machine.Bus.Read(0xA000));
    }
}
