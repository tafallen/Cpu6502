namespace Cpu6502.Tests;

public class CycleNotificationTests : CpuFixture
{
    [Fact]
    public void Step_RaisesOnCyclesConsumed_WithInstructionCycles()
    {
        int? consumed = null;
        Cpu.OnCyclesConsumed = c => consumed = c;

        Load(0x0200, 0xEA); // NOP
        Step();

        Assert.Equal(2, consumed);
    }

    [Fact]
    public void Step_RaisesOnCyclesConsumed_WhenServicingInterrupt()
    {
        int? consumed = null;
        Cpu.OnCyclesConsumed = c => consumed = c;

        Ram.Write(0xFFFA, 0x00);
        Ram.Write(0xFFFB, 0x09); // NMI vector -> $0900
        Load(0x0200, 0xEA);

        Cpu.Nmi();
        Step();

        Assert.Equal(7, consumed);
    }
}

