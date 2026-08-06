using Cpu6502.Core;
using Machines.Communicator;
using Xunit;

namespace Machines.Communicator.Tests;

public class CommunicatorBootTests
{
    [Fact]
    public void CommunicatorMachine_BootSequence_ExecutesInstructionsWithoutCrashing()
    {
        var rom = new byte[0x8000]; // 32 KB ROM ($8000–$FFFF)

        // Set RESET vector at $FFFC/$FFFD -> $8000 (offset $7FFC in 32KB ROM)
        rom[0x7FFC] = 0x00;
        rom[0x7FFD] = 0x80;

        // Place boot code at $8000:
        // $8000: SEI       (78)
        // $8001: CLD       (D8)
        // $8002: LDX #$FF  (A2 FF)
        // $8004: TXS       (9A)
        // $8005: LDA #$77  (A9 77)
        // $8007: STA $0200 (8D 00 02)
        // $800A: NOP       (EA)
        rom[0x0000] = 0x78;
        rom[0x0001] = 0xD8;
        rom[0x0002] = 0xA2; rom[0x0003] = 0xFF;
        rom[0x0004] = 0x9A;
        rom[0x0005] = 0xA9; rom[0x0006] = 0x77;
        rom[0x0007] = 0x8D; rom[0x0008] = 0x00; rom[0x0009] = 0x02;
        rom[0x000A] = 0xEA;

        var machine = new CommunicatorMachine(rom);
        machine.Reset();

        // Verify initial PC set from RESET vector ($8000)
        Assert.Equal((ushort)0x8000, machine.Cpu.PC);

        // Run 1 frame (40,000 cycles at 2 MHz)
        machine.RunFrame();

        // Verify CPU executed boot sequence and modified RAM
        Assert.True(machine.Cpu.TotalCycles >= 40_000);
        Assert.True(machine.Cpu.PC != 0x8000);
        Assert.Equal(0x77, machine.Bus.Read(0x0200));
    }
}
