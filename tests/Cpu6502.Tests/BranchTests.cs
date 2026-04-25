using Xunit;

namespace Cpu6502.Tests;

public class BranchTests : CpuFixture
{
    // ── BNE ───────────────────────────────────────────────────────────────────

    [Fact]
    public void BNE_Taken_UpdatesPC()
    {
        // BNE with +4 offset: PC should advance to instruction after BNE + 4
        Load(0x0200,
            0xA9, 0x01,   // LDA #$01  (Z=0)
            0xD0, 0x04);  // BNE +4  → PC should be $0208
        Step(2);
        Assert.Equal(0x0208, Cpu.PC);
    }

    [Fact]
    public void BNE_NotTaken_AdvancesPC_Normally()
    {
        Load(0x0200,
            0xA9, 0x00,   // LDA #$00  (Z=1)
            0xD0, 0x04);  // BNE +4 (not taken)
        Step(2);
        Assert.Equal(0x0204, Cpu.PC);
    }

    [Fact]
    public void BNE_NotTaken_Costs2Cycles()
    {
        Load(0x0200, 0xA9, 0x00, 0xD0, 0x04);
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(2UL, Cpu.TotalCycles - after);
    }

    [Fact]
    public void BNE_Taken_SamePage_Costs3Cycles()
    {
        Load(0x0200, 0xA9, 0x01, 0xD0, 0x04); // no page cross
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(3UL, Cpu.TotalCycles - after);
    }

    [Fact]
    public void BNE_Taken_PageCross_Costs4Cycles()
    {
        // Place BNE at $02FD so +4 jumps to $0303 (page cross)
        Load(0x02FB,
            0xA9, 0x01,   // LDA #$01
            0xD0, 0x04);  // BNE +4  → $0303 (crosses page)
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(4UL, Cpu.TotalCycles - after);
    }

    // ── BEQ ───────────────────────────────────────────────────────────────────

    [Fact]
    public void BEQ_Taken_WhenZeroSet()
    {
        Load(0x0200,
            0xA9, 0x00,   // LDA #$00 (Z=1)
            0xF0, 0x02);  // BEQ +2
        Step(2);
        Assert.Equal(0x0206, Cpu.PC);
    }

    [Fact]
    public void BEQ_NotTaken_WhenZeroClear()
    {
        Load(0x0200,
            0xA9, 0x01,   // LDA #$01 (Z=0)
            0xF0, 0x02);  // BEQ +2 (not taken)
        Step(2);
        Assert.Equal(0x0204, Cpu.PC);
    }

    // ── BCC / BCS ─────────────────────────────────────────────────────────────

    [Fact]
    public void BCC_Taken_WhenCarryClear()
    {
        Load(0x0200, 0x18, 0x90, 0x02); // CLC, BCC +2
        Step(2);
        Assert.Equal(0x0205, Cpu.PC);
    }

    [Fact]
    public void BCS_Taken_WhenCarrySet()
    {
        Load(0x0200, 0x38, 0xB0, 0x02); // SEC, BCS +2
        Step(2);
        Assert.Equal(0x0205, Cpu.PC);
    }

    // ── BPL / BMI ─────────────────────────────────────────────────────────────

    [Fact]
    public void BPL_Taken_WhenNegativeClear()
    {
        Load(0x0200, 0xA9, 0x01, 0x10, 0x02); // LDA #$01, BPL +2
        Step(2);
        Assert.Equal(0x0206, Cpu.PC);
    }

    [Fact]
    public void BMI_Taken_WhenNegativeSet()
    {
        Load(0x0200, 0xA9, 0x80, 0x30, 0x02); // LDA #$80, BMI +2
        Step(2);
        Assert.Equal(0x0206, Cpu.PC);
    }

    // ── BVC / BVS ─────────────────────────────────────────────────────────────

    [Fact]
    public void BVC_Taken_WhenOverflowClear()
    {
        // After reset, V=false
        Load(0x0200, 0x50, 0x02); // BVC +2
        Step();
        Assert.Equal(0x0204, Cpu.PC);
    }

    [Fact]
    public void BVS_Taken_WhenOverflowSet()
    {
        // $50 + $50 = $A0, sets V
        Load(0x0200,
            0x18, 0xA9, 0x50, 0x69, 0x50, // CLC, LDA #$50, ADC #$50 → V=1
            0x70, 0x02);                    // BVS +2
        Step(4);
        Assert.Equal(0x0209, Cpu.PC);
    }

    // ── Backward branch ───────────────────────────────────────────────────────

    [Fact]
    public void BNE_Backward_NegativeOffset()
    {
        // BNE with -4 offset: $0204 − 4 + 2 = $0202
        Load(0x0202,
            0xA9, 0x01,     // LDA #$01
            0xD0, 0xFC);    // BNE $FC = -4 signed → target = $0204 + (-4) = $0200?
                            // Actually: PC after fetch is $0206, offset is -4 → $0202
        Step(2);
        // PC after BNE: PC was $0206, offset -4 → $0202
        Assert.Equal(0x0202, Cpu.PC);
    }
}
