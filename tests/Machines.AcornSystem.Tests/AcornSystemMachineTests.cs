using Cpu6502.Core;
using Machines.AcornSystem;
using Xunit;

namespace Machines.AcornSystem.Tests;

public class AcornSystemMachineTests
{
    private static byte[] CreateMockSystemRom()
    {
        var rom = new byte[0x2000]; // 8 KB System ROM ($E000–$FFFF)

        // RESET vector at $FFFC/$FFFD -> $E000 (offset $1FFC in 8KB ROM)
        rom[0x1FFC] = 0x00;
        rom[0x1FFD] = 0xE0;

        // Place NOP at $E000 (offset 0x0000)
        rom[0x0000] = 0xEA;

        return rom;
    }

    [Fact]
    public void AcornSystemMachine_Initialization_System1_Maps512BytesRam()
    {
        var rom = CreateMockSystemRom();
        var machine = new AcornSystemMachine(rom, model: AcornSystemModel.System1);

        Assert.Equal(AcornSystemModel.System1, machine.Model);
        Assert.Equal(0x0200, machine.Ram.Memory.Length);

        machine.Bus.Write(0x01FF, 0xAA);
        Assert.Equal(0xAA, machine.Bus.Read(0x01FF));

        // Address beyond 512B ($0200) returns open bus ($FF)
        machine.Bus.Write(0x0200, 0xBB);
        Assert.Equal(0xFF, machine.Bus.Read(0x0200));
    }

    [Fact]
    public void AcornSystemMachine_Initialization_System2_Maps1KBRam()
    {
        var rom = CreateMockSystemRom();
        var machine = new AcornSystemMachine(rom, model: AcornSystemModel.System2);

        Assert.Equal(AcornSystemModel.System2, machine.Model);
        Assert.Equal(0x0400, machine.Ram.Memory.Length);

        machine.Bus.Write(0x03FF, 0xCC);
        Assert.Equal(0xCC, machine.Bus.Read(0x03FF));
    }

    [Fact]
    public void AcornSystemMachine_Initialization_System3_Maps16KBRam()
    {
        var rom = CreateMockSystemRom();
        var machine = new AcornSystemMachine(rom, model: AcornSystemModel.System3);

        Assert.Equal(AcornSystemModel.System3, machine.Model);
        Assert.Equal(0x4000, machine.Ram.Memory.Length);

        machine.Bus.Write(0x3FFF, 0x12);
        Assert.Equal(0x12, machine.Bus.Read(0x3FFF));
    }

    [Fact]
    public void AcornSystemMachine_Initialization_System4_Maps32KBRam()
    {
        var rom = CreateMockSystemRom();
        var machine = new AcornSystemMachine(rom, model: AcornSystemModel.System4);

        Assert.Equal(AcornSystemModel.System4, machine.Model);
        Assert.Equal(0x8000, machine.Ram.Memory.Length);

        machine.Bus.Write(0x7FFF, 0x34);
        Assert.Equal(0x34, machine.Bus.Read(0x7FFF));
    }

    [Fact]
    public void AcornSystemMachine_Initialization_System5_Maps48KBRam()
    {
        var rom = CreateMockSystemRom();
        var machine = new AcornSystemMachine(rom, model: AcornSystemModel.System5);

        Assert.Equal(AcornSystemModel.System5, machine.Model);
        Assert.Equal(0xC000, machine.Ram.Memory.Length);

        machine.Bus.Write(0xBFFF, 0x56);
        Assert.Equal(0x56, machine.Bus.Read(0xBFFF));
    }

    [Fact]
    public void AcornSystemMachine_Reset_SetsPcFromResetVector()
    {
        var rom = CreateMockSystemRom();
        var machine = new AcornSystemMachine(rom, model: AcornSystemModel.System3);

        machine.Reset();

        Assert.Equal((ushort)0xE000, machine.Cpu.PC);
    }
}
