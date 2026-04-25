using Cpu6502.Core;
using Xunit;

namespace Cpu6502.Tests;

public class ResetTests : CpuFixture
{
    [Fact]
    public void Reset_LoadsPC_FromResetVector()
    {
        Ram.Write(0xFFFC, 0x00);
        Ram.Write(0xFFFD, 0x04);  // vector = $0400
        Cpu.Reset();
        Assert.Equal(0x0400, Cpu.PC);
    }

    [Fact]
    public void Reset_SetsStackPointer_To0xFD()
    {
        Cpu.Reset();
        Assert.Equal(0xFD, Cpu.SP);
    }

    [Fact]
    public void Reset_SetsInterruptDisable()
    {
        Cpu.Reset();
        Assert.True(Cpu.I);
    }

    [Fact]
    public void Reset_ClearsDecimalMode()
    {
        Cpu.Reset();
        Assert.False(Cpu.D);
    }

    [Fact]
    public void Reset_Costs7Cycles()
    {
        Cpu.Reset();
        Assert.Equal(7UL, Cpu.TotalCycles);
    }

    [Fact]
    public void GetStatus_UnusedBitAlwaysSet()
    {
        Cpu.Reset();
        Assert.NotEqual(0, Cpu.GetStatus() & 0x20);
    }

    [Fact]
    public void GetStatus_BreakBit_ControlledByParameter()
    {
        Cpu.Reset();
        Assert.Equal(0, Cpu.GetStatus(breakFlag: false) & 0x10);
        Assert.NotEqual(0, Cpu.GetStatus(breakFlag: true)  & 0x10);
    }

    [Fact]
    public void GetStatus_SetStatus_RoundTrip()
    {
        Load(0x0200,
            0x38,       // SEC  — sets C
            0xF8,       // SED  — sets D
            0x78,       // SEI  — sets I
            0xEA        // NOP
        );
        Step(3);
        byte status = Cpu.GetStatus();
        Assert.NotEqual(0, status & (byte)StatusFlags.Carry);
        Assert.NotEqual(0, status & (byte)StatusFlags.InterruptDisable);
        Assert.NotEqual(0, status & (byte)StatusFlags.Decimal);
    }
}
