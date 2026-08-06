using System;
using Cpu6502.Core;
using Machines.BbcMicro;
using Xunit;

namespace Machines.BbcMicro.Tests;

public class BbcModelVariantTests
{
    private static (byte[] osRom, byte[] basicRom) CreateMockRoms()
    {
        var osRom = new byte[0x4000];
        var basicRom = new byte[0x4000];

        // Set RESET vector in OS ROM ($FFFC/$FFFD -> $C000)
        osRom[0x3FFC] = 0x00;
        osRom[0x3FFD] = 0xC0;

        return (osRom, basicRom);
    }

    [Fact]
    public void BbcMicroMachine_ModelA_Maps16KBMainRam()
    {
        var (osRom, basicRom) = CreateMockRoms();
        var machine = new BbcMicroMachine(osRom, basicRom, model: BbcModel.ModelA);

        Assert.Equal(BbcModel.ModelA, machine.Model);
        Assert.Equal(0x4000, machine.Ram.Memory.Length);

        // Address at $3FFF should be RAM (mapped)
        machine.Bus.Write(0x3FFF, 0x55);
        Assert.Equal(0x55, machine.Bus.Read(0x3FFF));

        // Address at $4000 should be open bus ($FF) on Model A
        machine.Bus.Write(0x4000, 0xAA);
        Assert.Equal(0xFF, machine.Bus.Read(0x4000));
    }

    [Fact]
    public void BbcMicroMachine_ModelB_Maps32KBMainRam()
    {
        var (osRom, basicRom) = CreateMockRoms();
        var machine = new BbcMicroMachine(osRom, basicRom, model: BbcModel.ModelB);

        Assert.Equal(BbcModel.ModelB, machine.Model);
        Assert.Equal(0x8000, machine.Ram.Memory.Length);

        // Address at $4000 and $7FFF should be RAM (mapped)
        machine.Bus.Write(0x4000, 0xAA);
        Assert.Equal(0xAA, machine.Bus.Read(0x4000));

        machine.Bus.Write(0x7FFF, 0xBB);
        Assert.Equal(0xBB, machine.Bus.Read(0x7FFF));
    }

    [Fact]
    public void BbcMicroMachine_ModelBPlus64_Maps64KBRam()
    {
        var (osRom, basicRom) = CreateMockRoms();
        var machine = new BbcMicroMachine(osRom, basicRom, model: BbcModel.ModelBPlus64);

        Assert.Equal(BbcModel.ModelBPlus64, machine.Model);
        Assert.Equal(0x10000, machine.Ram.Memory.Length);

        // Address at $7FFF should be Main RAM
        machine.Bus.Write(0x7FFF, 0x77);
        Assert.Equal(0x77, machine.Bus.Read(0x7FFF));
    }
}
