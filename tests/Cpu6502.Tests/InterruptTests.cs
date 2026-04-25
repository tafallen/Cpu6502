using Xunit;

namespace Cpu6502.Tests;

public class InterruptTests : CpuFixture
{
    // ── IRQ ───────────────────────────────────────────────────────────────────

    [Fact]
    public void IRQ_Masked_WhenInterruptDisableSet()
    {
        Ram.Write(0xFFFE, 0x00); Ram.Write(0xFFFF, 0x08); // IRQ vector $0800
        Load(0x0200, 0xEA, 0xEA); // NOP, NOP
        // I is set after Reset
        Cpu.Irq();
        Step();  // should execute NOP, not service IRQ
        Assert.Equal(0x0201, Cpu.PC);
    }

    [Fact]
    public void IRQ_Serviced_WhenInterruptDisableClear()
    {
        Ram.Write(0xFFFE, 0x00); Ram.Write(0xFFFF, 0x08); // IRQ vector $0800
        Load(0x0200, 0x58, 0xEA); // CLI, NOP
        Step();  // CLI — clears I
        Cpu.Irq();
        Step();  // service IRQ
        Assert.Equal(0x0800, Cpu.PC);
    }

    [Fact]
    public void IRQ_SetsInterruptDisable()
    {
        Ram.Write(0xFFFE, 0x00); Ram.Write(0xFFFF, 0x08);
        Load(0x0200, 0x58, 0xEA); // CLI, NOP
        Step();  // CLI
        Cpu.Irq();
        Step();  // service IRQ
        Assert.True(Cpu.I);
    }

    [Fact]
    public void IRQ_PushesPC_And_StatusWithBreakClear()
    {
        Ram.Write(0xFFFE, 0x00); Ram.Write(0xFFFF, 0x08);
        Load(0x0200, 0x58, 0xEA); // CLI, NOP
        Step();  // CLI
        byte spBefore = Cpu.SP;
        Cpu.Irq();
        Step();
        // Status byte pushed is at SP+1 (the third byte pushed)
        byte status = Ram.Read((ushort)(0x0100 | (byte)(spBefore - 2)));
        Assert.Equal(0, status & 0x10); // B must be clear for IRQ
    }

    [Fact]
    public void IRQ_Costs7Cycles()
    {
        Ram.Write(0xFFFE, 0x00); Ram.Write(0xFFFF, 0x08);
        Load(0x0200, 0x58, 0xEA);
        Step();  // CLI
        Cpu.Irq();
        var before = CycleSnapshot();
        Step();  // service IRQ
        Assert.Equal(7UL, Cpu.TotalCycles - before);
    }

    // ── NMI ───────────────────────────────────────────────────────────────────

    [Fact]
    public void NMI_AlwaysServiced_EvenWhenInterruptDisableSet()
    {
        Ram.Write(0xFFFA, 0x00); Ram.Write(0xFFFB, 0x09); // NMI vector $0900
        Load(0x0200, 0xEA); // NOP  (I is set from Reset)
        Cpu.Nmi();
        Step();
        Assert.Equal(0x0900, Cpu.PC);
    }

    [Fact]
    public void NMI_SetsInterruptDisable()
    {
        Ram.Write(0xFFFA, 0x00); Ram.Write(0xFFFB, 0x09);
        Load(0x0200, 0x58, 0xEA); // CLI, NOP
        Step();  // CLI
        Cpu.Nmi();
        Step();
        Assert.True(Cpu.I);
    }

    [Fact]
    public void NMI_Costs7Cycles()
    {
        Ram.Write(0xFFFA, 0x00); Ram.Write(0xFFFB, 0x09);
        Load(0x0200, 0xEA);
        Cpu.Nmi();
        var before = CycleSnapshot();
        Step();
        Assert.Equal(7UL, Cpu.TotalCycles - before);
    }

    [Fact]
    public void NMI_ClearsAfterService()
    {
        Ram.Write(0xFFFA, 0x00); Ram.Write(0xFFFB, 0x09);
        Ram.Write(0x0900, 0xEA); // NOP at ISR
        Load(0x0200, 0xEA);
        Cpu.Nmi();
        Step();  // services NMI → PC = $0900
        Step();  // NOP at $0900 — NMI should not fire again
        Assert.Equal(0x0901, Cpu.PC);
    }
}
