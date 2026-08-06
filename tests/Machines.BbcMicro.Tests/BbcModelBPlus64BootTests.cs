using System;
using Cpu6502.Core;
using Machines.BbcMicro;
using Xunit;

namespace Machines.BbcMicro.Tests;

public class BbcModelBPlus64BootTests
{
    private static (byte[] osRom, byte[] basicRom) CreateMockRoms()
    {
        var osRom = new byte[0x4000];   // 16 KB OS 1.20 / B+ ROM ($C000–$FFFF)
        var basicRom = new byte[0x4000];// 16 KB BASIC ROM ($8000–$BFFF)

        // Set RESET vector in OS ROM ($FFFC/$FFFD -> $C000)
        osRom[0x3FFC] = 0x00;
        osRom[0x3FFD] = 0xC0;

        // Code at $C000:
        // $C000: SEI       (78)
        // $C001: LDX #$FF  (A2 FF)
        // $C003: TXS       (9A)
        // $C004: LDA #$42  (A9 42)
        // $C006: STA $0500 (8D 00 05 - Main RAM write)
        // $C009: NOP       (EA)
        osRom[0x0000] = 0x78;
        osRom[0x0001] = 0xA2; osRom[0x0002] = 0xFF;
        osRom[0x0003] = 0x9A;
        osRom[0x0004] = 0xA9; osRom[0x0005] = 0x42;
        osRom[0x0006] = 0x8D; osRom[0x0007] = 0x00; osRom[0x0008] = 0x05;
        osRom[0x0009] = 0xEA;

        return (osRom, basicRom);
    }

    [Fact]
    public void BbcMicroMachine_ModelBPlus64_BootSequence_ExecutesAndWritesMainRam()
    {
        var (osRom, basicRom) = CreateMockRoms();
        var machine = new BbcMicroMachine(osRom, basicRom, model: BbcModel.ModelBPlus64);

        machine.Reset();

        // Verify initial PC set from RESET vector ($C000)
        Assert.Equal((ushort)0xC000, machine.Cpu.PC);

        // Run 1 frame (40,000 cycles at 2 MHz)
        machine.RunFrame();

        // Verify CPU progressed and executed reset sequence into 64KB RAM space
        Assert.True(machine.Cpu.TotalCycles >= 40_000);
        Assert.True(machine.Cpu.PC != 0xC000);
        Assert.Equal(0x42, machine.Bus.Read(0x0500));
    }
}
