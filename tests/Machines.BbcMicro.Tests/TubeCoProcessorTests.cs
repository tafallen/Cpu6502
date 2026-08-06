using Cpu6502.Core;
using Machines.BbcMicro;
using Xunit;

namespace Machines.BbcMicro.Tests;

public class TubeCoProcessorTests
{
    private static (byte[] osRom, byte[] basicRom) CreateMockRoms()
    {
        var osRom = new byte[0x4000];
        var basicRom = new byte[0x4000];
        osRom[0x3FFC] = 0x00;
        osRom[0x3FFD] = 0xC0;
        return (osRom, basicRom);
    }

    [Fact]
    public void BbcMicroMachine_WithTubeCoProcessor_Instantiates65C02Parasite()
    {
        var (osRom, basicRom) = CreateMockRoms();
        var machine = new BbcMicroMachine(osRom, basicRom, model: BbcModel.ModelB, enableCoProcessor: true);

        Assert.NotNull(machine.CoProcessor);
        Assert.NotNull(machine.CoProcessor.Cpu);
        Assert.Equal(0x10000, machine.CoProcessor.ParasiteRam.Memory.Length);

        // Verify Parasite RAM write
        machine.CoProcessor.ParasiteRam.Write(0x1000, 0x99);
        Assert.Equal(0x99, machine.CoProcessor.ParasiteRam.Read(0x1000));
    }

    [Fact]
    public void BbcMicroMachine_Step_TicksCoProcessorAt2xRate()
    {
        var (osRom, basicRom) = CreateMockRoms();
        var machine = new BbcMicroMachine(osRom, basicRom, model: BbcModel.ModelB, enableCoProcessor: true);

        // Place NOPs in Parasite RAM at $0000
        byte[]? parasiteRamBuf = machine.CoProcessor!.ParasiteRam.DirectWriteBuffer;
        if (parasiteRamBuf is not null)
        {
            Array.Fill(parasiteRamBuf, (byte)0xEA); // 0xEA = NOP
        }

        machine.Reset();
        machine.Step();

        // 1 host CPU step executes 2 Parasite CPU steps (2× clock rate)
        Assert.True(machine.CoProcessor.Cpu.TotalCycles >= 4);
    }
}
