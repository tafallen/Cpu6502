using Xunit;

namespace Cpu6502.Tests;

public class CompareTests : CpuFixture
{
    // ── CMP ───────────────────────────────────────────────────────────────────

    [Fact]
    public void CMP_Equal_SetsZeroAndCarry()
    {
        Load(0x0200, 0xA9, 0x42, 0xC9, 0x42); // LDA #$42, CMP #$42
        Step(2);
        Assert.True(Cpu.Z);
        Assert.True(Cpu.C);
        Assert.False(Cpu.N);
    }

    [Fact]
    public void CMP_Greater_SetsCarry_ClearsZero()
    {
        Load(0x0200, 0xA9, 0x50, 0xC9, 0x10); // LDA #$50, CMP #$10
        Step(2);
        Assert.True(Cpu.C);
        Assert.False(Cpu.Z);
    }

    [Fact]
    public void CMP_Less_ClearsCarry_SetsNegative()
    {
        Load(0x0200, 0xA9, 0x10, 0xC9, 0x50); // LDA #$10, CMP #$50 → result $C0 (N set)
        Step(2);
        Assert.False(Cpu.C);
        Assert.True(Cpu.N);
    }

    [Fact]
    public void CMP_DoesNotModifyAccumulator()
    {
        Load(0x0200, 0xA9, 0x42, 0xC9, 0x99); // LDA #$42, CMP #$99
        Step(2);
        Assert.Equal(0x42, Cpu.A);
    }

    [Fact]
    public void CMP_Immediate_Costs2Cycles()
    {
        Load(0x0200, 0xA9, 0x00, 0xC9, 0x00);
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(2UL, Cpu.TotalCycles - after);
    }

    // ── CPX ───────────────────────────────────────────────────────────────────

    [Fact]
    public void CPX_Equal_SetsZeroAndCarry()
    {
        Load(0x0200, 0xA2, 0x30, 0xE0, 0x30); // LDX #$30, CPX #$30
        Step(2);
        Assert.True(Cpu.Z);
        Assert.True(Cpu.C);
    }

    [Fact]
    public void CPX_DoesNotModifyX()
    {
        Load(0x0200, 0xA2, 0x10, 0xE0, 0x99);
        Step(2);
        Assert.Equal(0x10, Cpu.X);
    }

    // ── CPY ───────────────────────────────────────────────────────────────────

    [Fact]
    public void CPY_Equal_SetsZeroAndCarry()
    {
        Load(0x0200, 0xA0, 0x20, 0xC0, 0x20); // LDY #$20, CPY #$20
        Step(2);
        Assert.True(Cpu.Z);
        Assert.True(Cpu.C);
    }

    [Fact]
    public void CPY_Less_ClearsCarry()
    {
        Load(0x0200, 0xA0, 0x10, 0xC0, 0x20); // LDY #$10, CPY #$20
        Step(2);
        Assert.False(Cpu.C);
    }
}
