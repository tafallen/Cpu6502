using Xunit;

namespace Cpu6502.Tests;

public class FlagTests : CpuFixture
{
    [Fact]
    public void CLC_ClearsCarry()
    {
        Load(0x0200, 0x38, 0x18); // SEC, CLC
        Step(2);
        Assert.False(Cpu.C);
    }

    [Fact]
    public void SEC_SetsCarry()
    {
        Load(0x0200, 0x38); // SEC
        Step();
        Assert.True(Cpu.C);
    }

    [Fact]
    public void CLI_ClearsInterruptDisable()
    {
        Load(0x0200, 0x58); // CLI
        Step();
        Assert.False(Cpu.I);
    }

    [Fact]
    public void SEI_SetsInterruptDisable()
    {
        Load(0x0200, 0x58, 0x78); // CLI, SEI
        Step(2);
        Assert.True(Cpu.I);
    }

    [Fact]
    public void CLD_ClearsDecimal()
    {
        Load(0x0200, 0xF8, 0xD8); // SED, CLD
        Step(2);
        Assert.False(Cpu.D);
    }

    [Fact]
    public void SED_SetsDecimal()
    {
        Load(0x0200, 0xF8); // SED
        Step();
        Assert.True(Cpu.D);
    }

    [Fact]
    public void CLV_ClearsOverflow()
    {
        // Set V via ADC overflow, then CLV
        Load(0x0200,
            0x18, 0xA9, 0x50, 0x69, 0x50, // CLC, LDA #$50, ADC #$50 → V=1
            0xB8);                          // CLV
        Step(3);              // CLC, LDA, ADC — V should now be set
        Assert.True(Cpu.V);
        Step();               // CLV
        Assert.False(Cpu.V);
    }

    [Theory]
    [InlineData(new byte[] { 0x18 }, 2)]  // CLC
    [InlineData(new byte[] { 0x38 }, 2)]  // SEC
    [InlineData(new byte[] { 0x58 }, 2)]  // CLI
    [InlineData(new byte[] { 0x78 }, 2)]  // SEI
    [InlineData(new byte[] { 0xD8 }, 2)]  // CLD
    [InlineData(new byte[] { 0xF8 }, 2)]  // SED
    [InlineData(new byte[] { 0xB8 }, 2)]  // CLV
    [InlineData(new byte[] { 0xEA }, 2)]  // NOP
    public void FlagInstruction_Costs2Cycles(byte[] program, int expectedCycles)
    {
        Load(0x0200, program);
        var before = CycleSnapshot();
        Step();
        Assert.Equal((ulong)expectedCycles, Cpu.TotalCycles - before);
    }

    [Fact]
    public void NOP_ChangesNothing()
    {
        Load(0x0200, 0xA9, 0x42, 0xEA); // LDA #$42, NOP
        Step(2);
        Assert.Equal(0x42, Cpu.A);
        Assert.False(Cpu.Z);
        Assert.False(Cpu.N);
    }
}
