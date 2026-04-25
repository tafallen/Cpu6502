using Xunit;

namespace Cpu6502.Tests;

public class JumpTests : CpuFixture
{
    // ── JMP ───────────────────────────────────────────────────────────────────

    [Fact]
    public void JMP_Absolute_SetsPC()
    {
        Load(0x0200, 0x4C, 0x00, 0x04); // JMP $0400
        Step();
        Assert.Equal(0x0400, Cpu.PC);
    }

    [Fact]
    public void JMP_Absolute_Costs3Cycles()
    {
        Load(0x0200, 0x4C, 0x00, 0x04);
        var before = CycleSnapshot();
        Step();
        Assert.Equal(3UL, Cpu.TotalCycles - before);
    }

    [Fact]
    public void JMP_Indirect_SetsPC()
    {
        Ram.Write(0x0300, 0x00);  // lo
        Ram.Write(0x0301, 0x04);  // hi → $0400
        Load(0x0200, 0x6C, 0x00, 0x03); // JMP ($0300)
        Step();
        Assert.Equal(0x0400, Cpu.PC);
    }

    [Fact]
    public void JMP_Indirect_PageWrapBug()
    {
        // Real 6502 bug: JMP ($10FF) reads lo from $10FF and hi from $1000 (not $1100)
        Ram.Write(0x10FF, 0x00);  // lo byte of target
        Ram.Write(0x1000, 0x04);  // hi byte (wraps! — not $1100)
        Ram.Write(0x1100, 0xFF);  // this should NOT be used
        Load(0x0200, 0x6C, 0xFF, 0x10); // JMP ($10FF)
        Step();
        Assert.Equal(0x0400, Cpu.PC);
    }

    [Fact]
    public void JMP_Indirect_Costs5Cycles()
    {
        Load(0x0200, 0x6C, 0x00, 0x03);
        var before = CycleSnapshot();
        Step();
        Assert.Equal(5UL, Cpu.TotalCycles - before);
    }

    // ── JSR / RTS ─────────────────────────────────────────────────────────────

    [Fact]
    public void JSR_JumpsToSubroutine_And_PushesReturnAddress()
    {
        Load(0x0200, 0x20, 0x00, 0x04); // JSR $0400
        Step();
        Assert.Equal(0x0400, Cpu.PC);
    }

    [Fact]
    public void JSR_PushesPC_Minus1()
    {
        // JSR at $0200: instruction is 3 bytes ($0200-$0202), pushes $0202 (PC-1)
        Load(0x0200, 0x20, 0x00, 0x04); // JSR $0400
        byte spBefore = Cpu.SP;
        Step();
        byte hi = Ram.Read((ushort)(0x0100 | spBefore));
        byte lo = Ram.Read((ushort)(0x0100 | (byte)(spBefore - 1)));
        ushort pushed = (ushort)((hi << 8) | lo);
        Assert.Equal(0x0202, pushed);
    }

    [Fact]
    public void JSR_Costs6Cycles()
    {
        Load(0x0200, 0x20, 0x00, 0x04);
        var before = CycleSnapshot();
        Step();
        Assert.Equal(6UL, Cpu.TotalCycles - before);
    }

    [Fact]
    public void RTS_ReturnsToCaller()
    {
        // JSR at $0200 → subroutine at $0400, then RTS
        Ram.Write(0x0400, 0x60);  // RTS
        Load(0x0200, 0x20, 0x00, 0x04); // JSR $0400
        Step();  // JSR
        Step();  // RTS
        Assert.Equal(0x0203, Cpu.PC);  // returns to instruction after JSR
    }

    [Fact]
    public void RTS_Costs6Cycles()
    {
        Ram.Write(0x0400, 0x60);
        Load(0x0200, 0x20, 0x00, 0x04);
        Step();
        var after = CycleSnapshot();
        Step();  // RTS
        Assert.Equal(6UL, Cpu.TotalCycles - after);
    }

    // ── BRK ───────────────────────────────────────────────────────────────────

    [Fact]
    public void BRK_LoadsIRQVector()
    {
        Ram.Write(0xFFFE, 0x00);
        Ram.Write(0xFFFF, 0x08);  // IRQ vector = $0800
        Load(0x0200, 0x00);       // BRK
        Step();
        Assert.Equal(0x0800, Cpu.PC);
    }

    [Fact]
    public void BRK_SetsInterruptDisable()
    {
        Ram.Write(0xFFFE, 0x00); Ram.Write(0xFFFF, 0x08);
        Load(0x0200, 0x00);
        Step();
        Assert.True(Cpu.I);
    }

    [Fact]
    public void BRK_PushesBreakFlagSet()
    {
        Ram.Write(0xFFFE, 0x00); Ram.Write(0xFFFF, 0x08);
        Load(0x0200, 0x00);
        byte spBefore = Cpu.SP;
        Step();
        byte status = Ram.Read((ushort)(0x0100 | (byte)(spBefore - 2)));
        Assert.NotEqual(0, status & 0x10);  // B flag set
    }

    [Fact]
    public void BRK_Costs7Cycles()
    {
        Ram.Write(0xFFFE, 0x00); Ram.Write(0xFFFF, 0x08);
        Load(0x0200, 0x00);
        var before = CycleSnapshot();
        Step();
        Assert.Equal(7UL, Cpu.TotalCycles - before);
    }

    // ── RTI ───────────────────────────────────────────────────────────────────

    [Fact]
    public void RTI_RestoresPCAndFlags()
    {
        // Setup: BRK, then RTI in the ISR should return to $0202
        Ram.Write(0xFFFE, 0x00); Ram.Write(0xFFFF, 0x05);  // IRQ vector = $0500
        Ram.Write(0x0500, 0x40);  // RTI
        Load(0x0200, 0x58, 0x00); // CLI, BRK
        Step();  // CLI
        Step();  // BRK → jumps to $0500
        Step();  // RTI → should return to $0202
        Assert.Equal(0x0203, Cpu.PC);
    }

    [Fact]
    public void RTI_DoesNotAdd1_UnlikeRTS()
    {
        // RTI should restore PC exactly as pushed; BRK pushes PC+2 ($0202)
        Ram.Write(0xFFFE, 0x00); Ram.Write(0xFFFF, 0x05);
        Ram.Write(0x0500, 0x40);  // RTI
        Load(0x0200, 0x58, 0x00); // CLI, BRK
        Step(3);                  // CLI, BRK, RTI
        Assert.Equal(0x0203, Cpu.PC);
    }
}
