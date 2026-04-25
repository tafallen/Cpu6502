using Xunit;

namespace Cpu6502.Tests;

public class TransferTests : CpuFixture
{
    // ── Register transfers ────────────────────────────────────────────────────

    [Fact]
    public void TAX_TransfersAtoX_SetsZN()
    {
        Load(0x0200, 0xA9, 0x42, 0xAA); // LDA #$42, TAX
        Step(2);
        Assert.Equal(0x42, Cpu.X);
        Assert.False(Cpu.Z); Assert.False(Cpu.N);
    }

    [Fact]
    public void TAX_SetsZero_WhenAIsZero()
    {
        Load(0x0200, 0xA9, 0x00, 0xAA); // LDA #$00, TAX
        Step(2);
        Assert.True(Cpu.Z);
    }

    [Fact]
    public void TAX_SetsNegative_WhenBit7Set()
    {
        Load(0x0200, 0xA9, 0x80, 0xAA); // LDA #$80, TAX
        Step(2);
        Assert.True(Cpu.N);
    }

    [Fact]
    public void TAX_Costs2Cycles()
    {
        Load(0x0200, 0xA9, 0x00, 0xAA);
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(2UL, Cpu.TotalCycles - after);
    }

    [Fact]
    public void TXA_TransfersXtoA()
    {
        Load(0x0200, 0xA2, 0x33, 0x8A); // LDX #$33, TXA
        Step(2);
        Assert.Equal(0x33, Cpu.A);
    }

    [Fact]
    public void TAY_TransfersAtoY()
    {
        Load(0x0200, 0xA9, 0x55, 0xA8); // LDA #$55, TAY
        Step(2);
        Assert.Equal(0x55, Cpu.Y);
    }

    [Fact]
    public void TYA_TransfersYtoA()
    {
        Load(0x0200, 0xA0, 0x66, 0x98); // LDY #$66, TYA
        Step(2);
        Assert.Equal(0x66, Cpu.A);
    }

    [Fact]
    public void TSX_TransfersSPtoX_SetsZN()
    {
        Load(0x0200, 0xBA); // TSX
        Step();
        Assert.Equal(Cpu.SP, Cpu.X);  // SP was $FD after reset
        Assert.False(Cpu.Z);
        Assert.True(Cpu.N);  // $FD has bit 7 set
    }

    [Fact]
    public void TXS_TransfersXtoSP_DoesNotSetFlags()
    {
        Load(0x0200,
            0xA2, 0xFF,  // LDX #$FF  (sets N)
            0x9A);       // TXS
        Step(2);
        Assert.Equal(0xFF, Cpu.SP);
        // TXS must not have cleared N from the LDX
        Assert.True(Cpu.N);
    }

    // ── Stack ─────────────────────────────────────────────────────────────────

    [Fact]
    public void PHA_PLA_RoundTrip()
    {
        Load(0x0200,
            0xA9, 0xAB,  // LDA #$AB
            0x48,        // PHA
            0xA9, 0x00,  // LDA #$00  (clobber A)
            0x68);       // PLA
        Step(4);
        Assert.Equal(0xAB, Cpu.A);
    }

    [Fact]
    public void PHA_DecrementsSP()
    {
        Load(0x0200, 0xA9, 0x01, 0x48); // LDA #$01, PHA
        byte spBefore = Cpu.SP;
        Step(2);
        Assert.Equal((byte)(spBefore - 1), Cpu.SP);
    }

    [Fact]
    public void PLA_SetsZero_WhenPulled0()
    {
        Load(0x0200,
            0xA9, 0x00,  // LDA #$00
            0x48,        // PHA
            0x68);       // PLA
        Step(3);
        Assert.True(Cpu.Z);
    }

    [Fact]
    public void PLA_SetsNegative_WhenBit7Set()
    {
        Load(0x0200,
            0xA9, 0x80,  // LDA #$80
            0x48,        // PHA
            0xA9, 0x00,  // LDA #$00
            0x68);       // PLA
        Step(4);
        Assert.True(Cpu.N);
    }

    [Fact]
    public void PHA_Costs3Cycles()
    {
        Load(0x0200, 0xA9, 0x01, 0x48);
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(3UL, Cpu.TotalCycles - after);
    }

    [Fact]
    public void PLA_Costs4Cycles()
    {
        Load(0x0200, 0xA9, 0x01, 0x48, 0x68);
        Step(2);
        var after = CycleSnapshot();
        Step();
        Assert.Equal(4UL, Cpu.TotalCycles - after);
    }

    [Fact]
    public void PHP_PLP_RoundTrip()
    {
        Load(0x0200,
            0x38,  // SEC  — set C
            0x08,  // PHP
            0x18,  // CLC  — clear C
            0x28); // PLP  — should restore C=1
        Step(4);
        Assert.True(Cpu.C);
    }

    [Fact]
    public void PHP_PushesBreakAndUnusedBitsSet()
    {
        Load(0x0200, 0x08);  // PHP
        byte spBefore = Cpu.SP;
        Step();
        byte pushed = Ram.Read((ushort)(0x0100 | (byte)(spBefore)));
        Assert.NotEqual(0, pushed & 0x10);  // B set
        Assert.NotEqual(0, pushed & 0x20);  // Unused set
    }
}
