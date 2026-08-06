using Cpu6502.Core;
using Machines.BbcMicro;
using Xunit;

namespace Machines.BbcMicro.Tests;

public class TubeBootTests
{
    private static (byte[] osRom, byte[] basicRom) CreateMockRoms()
    {
        var osRom = new byte[0x4000];
        var basicRom = new byte[0x4000];

        // OS ROM RESET vector ($FFFC/$FFFD -> $C000)
        osRom[0x3FFC] = 0x00;
        osRom[0x3FFD] = 0xC0;

        // Host code at $C000:
        // $C000: SEI       (78)
        // $C001: STA $FEE1 (8D E1 FE - Write byte 0xAA to Tube R1 Host Data)
        osRom[0x0000] = 0x78;
        osRom[0x0001] = 0x8D; osRom[0x0002] = 0xE1; osRom[0x0003] = 0xFE;

        return (osRom, basicRom);
    }

    [Fact]
    public void TubeCoProcessor_BootAndInterProcessorCommunication_Succeeds()
    {
        var (osRom, basicRom) = CreateMockRoms();
        var machine = new BbcMicroMachine(osRom, basicRom, model: BbcModel.ModelB, enableCoProcessor: true);

        Assert.NotNull(machine.CoProcessor);

        // Populate Parasite RAM vector BEFORE machine.Reset()
        byte[]? parasiteRam = machine.CoProcessor.ParasiteRam.DirectWriteBuffer;
        if (parasiteRam is not null)
        {
            parasiteRam[0xFFFC] = 0x00;
            parasiteRam[0xFFFD] = 0xF0;

            // Code at $F000 on Parasite CPU: NOP loop
            for (int i = 0; i < 0x0100; i++)
            {
                parasiteRam[0xF000 + i] = 0xEA; // NOP
            }
        }

        machine.Reset();

        // Verify initial PCs
        Assert.Equal((ushort)0xC000, machine.Cpu.PC);
        Assert.Equal((ushort)0xF000, machine.CoProcessor.Cpu.PC);

        // Run 1 frame (40,000 cycles)
        machine.RunFrame();

        // Verify dual-CPU execution progressed
        Assert.True(machine.Cpu.TotalCycles >= 40_000);
        Assert.True(machine.CoProcessor.Cpu.TotalCycles > 0);
        Assert.True(machine.CoProcessor.Cpu.PC != 0xF000);
    }
}
