using Cpu6502.Core;
using Machines.AcornSystem;
using Xunit;

namespace Machines.AcornSystem.Tests;

public class AcornSystemBootTests
{
    [Fact]
    public void AcornSystemMachine_BootSequence_ExecutesInstructionsWithoutCrashing()
    {
        var rom = new byte[0x2000]; // 8 KB System ROM ($E000–$FFFF)

        // Set RESET vector at $FFFC/$FFFD -> $E000 (offset $1FFC in 8KB ROM)
        rom[0x1FFC] = 0x00;
        rom[0x1FFD] = 0xE0;

        // Place a boot sequence at $E000:
        // $E000: SEI          (78)
        // $E001: CLD          (D8)
        // $E002: LDX #$FF     (A2 FF)
        // $E004: TXS          (9A)
        // $E005: LDA #$55     (A9 55)
        // $E007: STA $0200    (8D 00 02 - Write to System RAM)
        // $E00A: NOP          (EA)
        rom[0x0000] = 0x78;
        rom[0x0001] = 0xD8;
        rom[0x0002] = 0xA2; rom[0x0003] = 0xFF;
        rom[0x0004] = 0x9A;
        rom[0x0005] = 0xA9; rom[0x0006] = 0x55;
        rom[0x0007] = 0x8D; rom[0x0008] = 0x00; rom[0x0009] = 0x02;
        rom[0x000A] = 0xEA;

        var machine = new AcornSystemMachine(rom, model: AcornSystemModel.System3);
        machine.Reset();

        // Verify PC set from RESET vector ($E000)
        Assert.Equal((ushort)0xE000, machine.Cpu.PC);

        // Run 1 frame (20,000 cycles)
        machine.RunFrame();

        // Verify CPU executed boot sequence, updated RAM, and advanced cycles
        Assert.True(machine.Cpu.TotalCycles >= 20_000);
        Assert.True(machine.Cpu.PC != 0xE000);
        Assert.Equal(0x55, machine.Bus.Read(0x0200));
    }
}
