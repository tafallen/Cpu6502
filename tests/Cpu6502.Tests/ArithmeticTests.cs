using Xunit;

namespace Cpu6502.Tests;

public class ArithmeticTests : CpuFixture
{
    // ── ADC ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ADC_Immediate_AddsWithoutCarry()
    {
        Load(0x0200, 0x18, 0xA9, 0x10, 0x69, 0x20); // CLC, LDA #$10, ADC #$20
        Step(3);
        Assert.Equal(0x30, Cpu.A);
        Assert.False(Cpu.C);
    }

    [Fact]
    public void ADC_Immediate_SetsCarryOnOverflow()
    {
        Load(0x0200, 0x38, 0xA9, 0xFF, 0x69, 0x01); // SEC, LDA #$FF, ADC #$01
        Step(3);
        Assert.Equal(0x01, Cpu.A);  // $FF + $01 + carry(1) = $101 → $01 with carry
        Assert.True(Cpu.C);
    }

    [Fact]
    public void ADC_SetsOverflowFlag_PositivePlusPositiveGoesNegative()
    {
        // $50 + $50 = $A0 — both positive, result has bit 7 set → signed overflow
        Load(0x0200, 0x18, 0xA9, 0x50, 0x69, 0x50); // CLC, LDA #$50, ADC #$50
        Step(3);
        Assert.True(Cpu.V);
        Assert.True(Cpu.N);
    }

    [Fact]
    public void ADC_ClearsOverflowFlag_WhenNoSignedOverflow()
    {
        Load(0x0200, 0x18, 0xA9, 0x01, 0x69, 0x01); // CLC, LDA #$01, ADC #$01
        Step(3);
        Assert.False(Cpu.V);
    }

    [Fact]
    public void ADC_SetsZeroFlag_WhenResultIsZero()
    {
        Load(0x0200, 0x18, 0xA9, 0xFF, 0x69, 0x01); // CLC, LDA #$FF, ADC #$01 → 0 + carry
        Step(3);
        Assert.True(Cpu.Z);
    }

    [Fact]
    public void ADC_Costs2Cycles_Immediate()
    {
        Load(0x0200, 0x18, 0xA9, 0x00, 0x69, 0x00); // CLC, LDA, ADC #$00
        Step(2);
        var after = CycleSnapshot();
        Step();
        Assert.Equal(2UL, Cpu.TotalCycles - after);
    }

    [Fact]
    public void ADC_BCD_AddsCorrectly()
    {
        Load(0x0200, 0xF8, 0x18, 0xA9, 0x09, 0x69, 0x01); // SED, CLC, LDA #$09, ADC #$01
        Step(4);
        Assert.Equal(0x10, Cpu.A);  // BCD: 9+1 = 10 → $10
    }

    // ── SBC ───────────────────────────────────────────────────────────────────

    [Fact]
    public void SBC_Immediate_SubtractsWithBorrow()
    {
        Load(0x0200, 0x38, 0xA9, 0x50, 0xE9, 0x10); // SEC, LDA #$50, SBC #$10
        Step(3);
        Assert.Equal(0x40, Cpu.A);
        Assert.True(Cpu.C);   // C=1 means no borrow
    }

    [Fact]
    public void SBC_ClearsCarry_WhenBorrowOccurs()
    {
        Load(0x0200, 0x38, 0xA9, 0x00, 0xE9, 0x01); // SEC, LDA #$00, SBC #$01
        Step(3);
        Assert.Equal(0xFF, Cpu.A);
        Assert.False(Cpu.C);  // borrow
    }

    [Fact]
    public void SBC_SetsOverflow_NegativeMinusPositiveGoesPositive()
    {
        // $80 (−128) − $10 (+16) = $70 (+112): negative minus positive → positive = overflow
        Load(0x0200, 0x38, 0xA9, 0x80, 0xE9, 0x10); // SEC, LDA #$80, SBC #$10
        Step(3);
        Assert.True(Cpu.V);
    }

    // ── INC / DEC (memory) ────────────────────────────────────────────────────

    [Fact]
    public void INC_ZeroPage_IncrementsMemory()
    {
        Ram.Write(0x0050, 0x09);
        Load(0x0200, 0xE6, 0x50); // INC $50
        Step();
        Assert.Equal(0x0A, Ram.Read(0x0050));
    }

    [Fact]
    public void INC_ZeroPage_WrapsFrom0xFF_To0x00_SetsZero()
    {
        Ram.Write(0x0050, 0xFF);
        Load(0x0200, 0xE6, 0x50); // INC $50
        Step();
        Assert.Equal(0x00, Ram.Read(0x0050));
        Assert.True(Cpu.Z);
    }

    [Fact]
    public void INC_ZeroPage_Costs5Cycles()
    {
        Load(0x0200, 0xE6, 0x50);
        var before = CycleSnapshot();
        Step();
        Assert.Equal(5UL, Cpu.TotalCycles - before);
    }

    [Fact]
    public void DEC_ZeroPage_DecrementsMemory()
    {
        Ram.Write(0x0050, 0x0A);
        Load(0x0200, 0xC6, 0x50); // DEC $50
        Step();
        Assert.Equal(0x09, Ram.Read(0x0050));
    }

    [Fact]
    public void DEC_ZeroPage_WrapsFrom0x00_To0xFF_SetsNegative()
    {
        Ram.Write(0x0050, 0x00);
        Load(0x0200, 0xC6, 0x50); // DEC $50
        Step();
        Assert.Equal(0xFF, Ram.Read(0x0050));
        Assert.True(Cpu.N);
    }

    // ── INX / INY / DEX / DEY ─────────────────────────────────────────────────

    [Fact]
    public void INX_IncrementsX()
    {
        Load(0x0200, 0xA2, 0x09, 0xE8); // LDX #$09, INX
        Step(2);
        Assert.Equal(0x0A, Cpu.X);
    }

    [Fact]
    public void INX_WrapsFrom0xFF_To0x00()
    {
        Load(0x0200, 0xA2, 0xFF, 0xE8); // LDX #$FF, INX
        Step(2);
        Assert.Equal(0x00, Cpu.X);
        Assert.True(Cpu.Z);
    }

    [Fact]
    public void DEX_DecrementsX()
    {
        Load(0x0200, 0xA2, 0x05, 0xCA); // LDX #$05, DEX
        Step(2);
        Assert.Equal(0x04, Cpu.X);
    }

    [Fact]
    public void INY_IncrementsY()
    {
        Load(0x0200, 0xA0, 0x04, 0xC8); // LDY #$04, INY
        Step(2);
        Assert.Equal(0x05, Cpu.Y);
    }

    [Fact]
    public void DEY_DecrementsY()
    {
        Load(0x0200, 0xA0, 0x01, 0x88); // LDY #$01, DEY
        Step(2);
        Assert.Equal(0x00, Cpu.Y);
        Assert.True(Cpu.Z);
    }

    [Fact]
    public void INX_Costs2Cycles()
    {
        Load(0x0200, 0xA2, 0x00, 0xE8);
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(2UL, Cpu.TotalCycles - after);
    }
}
