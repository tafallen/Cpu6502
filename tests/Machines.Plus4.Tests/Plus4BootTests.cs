using Cpu6502.Core;
using Machines.Plus4;
using Xunit;

namespace Machines.Plus4.Tests;

public class Plus4BootTests
{
    [Fact]
    public void Plus4Machine_BootSequence_ExecutesInstructionsWithoutCrashing()
    {
        var kernal = new byte[0x4000]; // 16 KB Kernal ($C000–$FFFF)
        var basic = new byte[0x4000];  // 16 KB BASIC ($8000–$BFFF)

        // Set RESET vector at $FFFC/$FFFD -> $E000 (offset $3FFC in 16KB Kernal)
        kernal[0x3FFC] = 0x00;
        kernal[0x3FFD] = 0xE0;

        // Place a boot sequence at $E000:
        // $E000: SEI          (78)
        // $E001: CLD          (D8)
        // $E002: LDX #$FF     (A2 FF)
        // $E004: TXS          (9A)
        // $E005: LDA #$00     (A9 00)
        // $E007: STA $FF15    (8D 15 FF - Set TED background color)
        // $E00A: NOP          (EA)
        kernal[0x2000] = 0x78;
        kernal[0x2001] = 0xD8;
        kernal[0x2002] = 0xA2; kernal[0x2003] = 0xFF;
        kernal[0x2004] = 0x9A;
        kernal[0x2005] = 0xA9; kernal[0x2006] = 0x00;
        kernal[0x2007] = 0x8D; kernal[0x2008] = 0x15; kernal[0x2009] = 0xFF;
        kernal[0x200A] = 0xEA;

        var machine = new Plus4Machine(kernal, basic);
        machine.Reset();

        // Verify initial PC set from RESET vector ($E000)
        Assert.Equal((ushort)0xE000, machine.Cpu.PC);

        // Run 1 frame (35,520 cycles)
        machine.RunFrame();

        // Verify CPU progressed and executed reset sequence
        Assert.True(machine.Cpu.TotalCycles >= 35_520);
        Assert.True(machine.Cpu.PC != 0xE000);
    }
}
