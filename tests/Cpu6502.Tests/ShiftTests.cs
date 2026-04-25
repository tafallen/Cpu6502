using Xunit;

namespace Cpu6502.Tests;

public class ShiftTests : CpuFixture
{
    // ── ASL ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ASL_Accumulator_ShiftsLeft()
    {
        Load(0x0200, 0xA9, 0x01, 0x0A); // LDA #$01, ASL A
        Step(2);
        Assert.Equal(0x02, Cpu.A);
        Assert.False(Cpu.C);
    }

    [Fact]
    public void ASL_Accumulator_ShiftsCarryFromBit7()
    {
        Load(0x0200, 0xA9, 0x81, 0x0A); // LDA #$81, ASL A
        Step(2);
        Assert.Equal(0x02, Cpu.A);
        Assert.True(Cpu.C);
    }

    [Fact]
    public void ASL_Accumulator_SetsNegative()
    {
        Load(0x0200, 0xA9, 0x40, 0x0A); // LDA #$40, ASL A → $80
        Step(2);
        Assert.True(Cpu.N);
    }

    [Fact]
    public void ASL_Accumulator_Costs2Cycles()
    {
        Load(0x0200, 0xA9, 0x01, 0x0A);
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(2UL, Cpu.TotalCycles - after);
    }

    [Fact]
    public void ASL_ZeroPage_ShiftsMemory()
    {
        Ram.Write(0x0050, 0x04);
        Load(0x0200, 0x06, 0x50); // ASL $50
        Step();
        Assert.Equal(0x08, Ram.Read(0x0050));
    }

    [Fact]
    public void ASL_ZeroPage_Costs5Cycles()
    {
        Load(0x0200, 0x06, 0x50);
        var before = CycleSnapshot();
        Step();
        Assert.Equal(5UL, Cpu.TotalCycles - before);
    }

    // ── LSR ───────────────────────────────────────────────────────────────────

    [Fact]
    public void LSR_Accumulator_ShiftsRight()
    {
        Load(0x0200, 0xA9, 0x04, 0x4A); // LDA #$04, LSR A
        Step(2);
        Assert.Equal(0x02, Cpu.A);
        Assert.False(Cpu.C);
    }

    [Fact]
    public void LSR_Accumulator_ShiftsCarryFromBit0()
    {
        Load(0x0200, 0xA9, 0x01, 0x4A); // LDA #$01, LSR A
        Step(2);
        Assert.Equal(0x00, Cpu.A);
        Assert.True(Cpu.C);
        Assert.True(Cpu.Z);
    }

    [Fact]
    public void LSR_Accumulator_AlwaysClearsNegative()
    {
        Load(0x0200, 0xA9, 0xFF, 0x4A); // LDA #$FF, LSR A → $7F
        Step(2);
        Assert.False(Cpu.N);
    }

    // ── ROL ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ROL_Accumulator_RotatesCarryIn()
    {
        Load(0x0200, 0x38, 0xA9, 0x00, 0x2A); // SEC, LDA #$00, ROL A
        Step(3);
        Assert.Equal(0x01, Cpu.A);  // carry rotated into bit 0
        Assert.False(Cpu.C);        // old bit 7 was 0 → carry out = 0
    }

    [Fact]
    public void ROL_Accumulator_RotatesCarryOut()
    {
        Load(0x0200, 0x18, 0xA9, 0x80, 0x2A); // CLC, LDA #$80, ROL A
        Step(3);
        Assert.Equal(0x00, Cpu.A);  // $80 << 1 = $00 with old carry(0) in bit 0
        Assert.True(Cpu.C);         // bit 7 was 1 → carry out
    }

    [Fact]
    public void ROL_Costs2Cycles_Accumulator()
    {
        Load(0x0200, 0x18, 0xA9, 0x01, 0x2A);
        Step(2);
        var after = CycleSnapshot();
        Step();
        Assert.Equal(2UL, Cpu.TotalCycles - after);
    }

    // ── ROR ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ROR_Accumulator_RotatesCarryIn()
    {
        Load(0x0200, 0x38, 0xA9, 0x00, 0x6A); // SEC, LDA #$00, ROR A
        Step(3);
        Assert.Equal(0x80, Cpu.A);  // carry → bit 7
        Assert.False(Cpu.C);        // old bit 0 was 0
    }

    [Fact]
    public void ROR_Accumulator_RotatesCarryOut()
    {
        Load(0x0200, 0x18, 0xA9, 0x01, 0x6A); // CLC, LDA #$01, ROR A
        Step(3);
        Assert.Equal(0x00, Cpu.A);
        Assert.True(Cpu.C);         // bit 0 was 1 → carry out
    }

    [Fact]
    public void ROR_ZeroPage_ShiftsMemory()
    {
        Ram.Write(0x0050, 0x04);
        Load(0x0200, 0x18, 0x66, 0x50); // CLC, ROR $50
        Step(2);
        Assert.Equal(0x02, Ram.Read(0x0050));
    }

    [Fact]
    public void ROR_ZeroPage_Costs5Cycles()
    {
        Load(0x0200, 0x18, 0x66, 0x50);
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(5UL, Cpu.TotalCycles - after);
    }
}
