using Cpu6502.Core;
using Machines.Communicator;
using Xunit;

namespace Machines.Communicator.Tests;

public class CommunicatorMachineTests
{
    private static byte[] CreateMockSystemRom()
    {
        var rom = new byte[0x8000]; // 32 KB ROM ($8000–$FFFF)

        // RESET vector at $FFFC/$FFFD -> $8000 (offset $7FFC in 32KB ROM)
        rom[0x7FFC] = 0x00;
        rom[0x7FFD] = 0x80;

        // Code at $8000:
        // $8000: SEI       (78)
        // $8001: LDX #$FF  (A2 FF)
        // $8003: TXS       (9A)
        // $8004: LDA #$88  (A9 88)
        // $8006: STA $0400 (8D 00 04)
        rom[0x0000] = 0x78;
        rom[0x0001] = 0xA2; rom[0x0002] = 0xFF;
        rom[0x0003] = 0x9A;
        rom[0x0004] = 0xA9; rom[0x0005] = 0x88;
        rom[0x0006] = 0x8D; rom[0x0007] = 0x00; rom[0x0008] = 0x04;

        return rom;
    }

    [Fact]
    public void CommunicatorMachine_Initialization_Maps32KBRamAnd32KBRom()
    {
        var rom = CreateMockSystemRom();
        var machine = new CommunicatorMachine(rom);

        Assert.Equal(0x8000, machine.Ram.Memory.Length);

        machine.Bus.Write(0x7FFF, 0x33);
        Assert.Equal(0x33, machine.Bus.Read(0x7FFF));
    }

    [Fact]
    public void CommunicatorMachine_BootSequence_ExecutesAndAccessesRam()
    {
        var rom = CreateMockSystemRom();
        var machine = new CommunicatorMachine(rom);

        machine.Reset();

        Assert.Equal((ushort)0x8000, machine.Cpu.PC);

        machine.RunFrame();

        Assert.True(machine.Cpu.TotalCycles >= 40_000);
        Assert.Equal(0x88, machine.Bus.Read(0x0400));
    }
}
