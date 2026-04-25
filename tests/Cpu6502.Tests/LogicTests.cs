using Xunit;

namespace Cpu6502.Tests;

public class LogicTests : CpuFixture
{
    // ── AND ───────────────────────────────────────────────────────────────────

    [Fact]
    public void AND_Immediate_MasksAccumulator()
    {
        Load(0x0200, 0xA9, 0xFF, 0x29, 0x0F); // LDA #$FF, AND #$0F
        Step(2);
        Assert.Equal(0x0F, Cpu.A);
    }

    [Fact]
    public void AND_SetsZero_WhenResultIsZero()
    {
        Load(0x0200, 0xA9, 0x55, 0x29, 0xAA); // LDA #$55, AND #$AA → 0
        Step(2);
        Assert.True(Cpu.Z);
    }

    [Fact]
    public void AND_SetsNegative_WhenBit7Set()
    {
        Load(0x0200, 0xA9, 0xFF, 0x29, 0x80); // LDA #$FF, AND #$80
        Step(2);
        Assert.True(Cpu.N);
    }

    [Fact]
    public void AND_Costs2Cycles_Immediate()
    {
        Load(0x0200, 0xA9, 0xFF, 0x29, 0xFF);
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(2UL, Cpu.TotalCycles - after);
    }

    // ── ORA ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ORA_Immediate_ORsAccumulator()
    {
        Load(0x0200, 0xA9, 0x0F, 0x09, 0xF0); // LDA #$0F, ORA #$F0
        Step(2);
        Assert.Equal(0xFF, Cpu.A);
    }

    [Fact]
    public void ORA_SetsZero_WhenBothZero()
    {
        Load(0x0200, 0xA9, 0x00, 0x09, 0x00);
        Step(2);
        Assert.True(Cpu.Z);
    }

    // ── EOR ───────────────────────────────────────────────────────────────────

    [Fact]
    public void EOR_Immediate_XORsAccumulator()
    {
        Load(0x0200, 0xA9, 0xFF, 0x49, 0x0F); // LDA #$FF, EOR #$0F
        Step(2);
        Assert.Equal(0xF0, Cpu.A);
    }

    [Fact]
    public void EOR_SetsZero_WhenResultIsZero()
    {
        Load(0x0200, 0xA9, 0xAA, 0x49, 0xAA); // LDA #$AA, EOR #$AA → 0
        Step(2);
        Assert.True(Cpu.Z);
    }

    // ── BIT ───────────────────────────────────────────────────────────────────

    [Fact]
    public void BIT_ZeroPage_SetsZero_WhenAndResultIsZero()
    {
        Ram.Write(0x0050, 0xF0);
        Load(0x0200, 0xA9, 0x0F, 0x24, 0x50); // LDA #$0F, BIT $50
        Step(2);
        Assert.True(Cpu.Z);
        Assert.Equal(0x0F, Cpu.A);  // BIT does NOT change A
    }

    [Fact]
    public void BIT_CopiesBit7ofMemory_ToNegativeFlag()
    {
        Ram.Write(0x0050, 0x80);
        Load(0x0200, 0xA9, 0xFF, 0x24, 0x50); // LDA #$FF, BIT $50
        Step(2);
        Assert.True(Cpu.N);
    }

    [Fact]
    public void BIT_CopiesBit6ofMemory_ToOverflowFlag()
    {
        Ram.Write(0x0050, 0x40);
        Load(0x0200, 0xA9, 0xFF, 0x24, 0x50); // LDA #$FF, BIT $50
        Step(2);
        Assert.True(Cpu.V);
    }

    [Fact]
    public void BIT_Absolute_Costs4Cycles()
    {
        Load(0x0200, 0xA9, 0xFF, 0x2C, 0x00, 0x03); // LDA #$FF, BIT $0300
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(4UL, Cpu.TotalCycles - after);
    }
}
