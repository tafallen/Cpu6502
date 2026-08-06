using System;
using Cpu6502.Core;
using Machines.Electron;
using Xunit;

namespace Machines.Electron.Tests;

public class ElectronMachineTests
{
    private static (byte[] osRom, byte[] basicRom) CreateMockRoms()
    {
        var osRom = new byte[0x4000];
        var basicRom = new byte[0x4000];

        // Initialize osRom with 0xFF default (unwritten ROM)
        Array.Fill(osRom, (byte)0xFF);

        // Set RESET vector in OS ROM ($FFFC/$FFFD -> $C000)
        // OS ROM spans $C000-$FFFF (size 0x4000). $FFFC - $C000 = 0x3FFC.
        osRom[0x3FFC] = 0x00;
        osRom[0x3FFD] = 0xC0;

        // Place NOP at $C000
        osRom[0x0000] = 0xEA;

        return (osRom, basicRom);
    }

    [Fact]
    public void ElectronMachine_Initialization_MapsMemoryCorrectly()
    {
        var (osRom, basicRom) = CreateMockRoms();
        var machine = new ElectronMachine(osRom, basicRom);

        Assert.NotNull(machine.Cpu);
        Assert.NotNull(machine.Ram);
        Assert.NotNull(machine.Ula);
        Assert.NotNull(machine.Bus);
    }

    [Fact]
    public void ElectronMachine_Reset_ResetsCpuAndSetsProgramCounter()
    {
        var (osRom, basicRom) = CreateMockRoms();
        var machine = new ElectronMachine(osRom: osRom, basicRom: basicRom);

        machine.Reset();

        Assert.Equal((ushort)0xC000, machine.Cpu.PC);
    }

    [Fact]
    public void ElectronMachine_RunFrame_ExecutesSpecifiedCycles()
    {
        var (osRom, basicRom) = CreateMockRoms();
        var machine = new ElectronMachine(osRom, basicRom);
        machine.Reset();

        ulong initialCycles = machine.Cpu.TotalCycles;
        machine.RunFrame(100);

        Assert.True(machine.Cpu.TotalCycles >= initialCycles + 100);
    }
}
