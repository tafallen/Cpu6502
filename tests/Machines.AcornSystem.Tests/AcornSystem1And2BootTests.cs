using Cpu6502.Core;
using Machines.AcornSystem;
using Xunit;

namespace Machines.AcornSystem.Tests;

public class AcornSystem1And2BootTests
{
    [Fact]
    public void AcornSystem1_BootSequence_ExecutesAndAccesses512BRam()
    {
        var rom = new byte[0x0200]; // 512B CUTS OS ROM ($FE00–$FFFF)

        // RESET vector at $FFFC/$FFFD -> $FE00 (offset $01FC in 512B ROM)
        rom[0x01FC] = 0x00;
        rom[0x01FD] = 0xFE;

        // Code at $FE00:
        // $FE00: SEI       (78)
        // $FE01: LDX #$FF  (A2 FF)
        // $FE03: TXS       (9A)
        // $FE04: LDA #$12  (A9 12)
        // $FE06: STA $0080 (85 80)
        rom[0x0000] = 0x78;
        rom[0x0001] = 0xA2; rom[0x0002] = 0xFF;
        rom[0x0003] = 0x9A;
        rom[0x0004] = 0xA9; rom[0x0005] = 0x12;
        rom[0x0006] = 0x85; rom[0x0007] = 0x80;
        rom[0x0008] = 0xEA;

        var machine = new AcornSystemMachine(rom, model: AcornSystemModel.System1);
        machine.Reset();

        Assert.Equal((ushort)0xFE00, machine.Cpu.PC);

        machine.RunFrame();

        Assert.True(machine.Cpu.TotalCycles >= 20_000);
        Assert.Equal(0x12, machine.Bus.Read(0x0080));
    }

    [Fact]
    public void AcornSystem2_BootSequence_ExecutesAndAccesses1KBRam()
    {
        var rom = new byte[0x0800]; // 2 KB OS ROM ($F800–$FFFF)

        // RESET vector at $FFFC/$FFFD -> $F800 (offset $07FC in 2KB ROM)
        rom[0x07FC] = 0x00;
        rom[0x07FD] = 0xF8;

        // Code at $F800:
        // $F800: SEI       (78)
        // $F801: LDX #$FF  (A2 FF)
        // $F803: TXS       (9A)
        // $F804: LDA #$34  (A9 34)
        // $F806: STA $03FF (8D FF 03)
        rom[0x0000] = 0x78;
        rom[0x0001] = 0xA2; rom[0x0002] = 0xFF;
        rom[0x0003] = 0x9A;
        rom[0x0004] = 0xA9; rom[0x0005] = 0x34;
        rom[0x0006] = 0x8D; rom[0x0007] = 0xFF; rom[0x0008] = 0x03;

        var machine = new AcornSystemMachine(rom, model: AcornSystemModel.System2);
        machine.Reset();

        Assert.Equal((ushort)0xF800, machine.Cpu.PC);

        machine.RunFrame();

        Assert.True(machine.Cpu.TotalCycles >= 20_000);
        Assert.Equal(0x34, machine.Bus.Read(0x03FF));
    }
}
