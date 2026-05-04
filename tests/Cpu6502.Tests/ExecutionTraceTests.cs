using Cpu6502.Core;
using Xunit;

namespace Cpu6502.Tests;

public class ExecutionTraceTests : CpuFixture
{
    // ── Basic Trace Setup Tests ──────────────────────────────────────────

    [Fact]
    public void Trace_DefaultIsNullTrace()
    {
        // Default Cpu.Trace should be NullTrace
        Assert.Same(NullTrace.Instance, Cpu.Trace);
    }

    [Fact]
    public void Trace_CanSetCustomTrace()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;
        Assert.Same(trace, Cpu.Trace);
    }

    [Fact]
    public void Trace_SetNullAssignsNullTrace()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;
        Cpu.Trace = null;
        Assert.Same(NullTrace.Instance, Cpu.Trace);
    }

    // ── Instruction Execution Tracing Tests ──────────────────────────────

    [Fact]
    public void Trace_CaptureLDAImmediate()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42);  // LDA #$42
        Step();

        // Verify instruction event captured
        Assert.Single(trace.Instructions);
        var instr = trace.Instructions[0];
        Assert.Equal(0x0200, instr.Pc);
        Assert.Equal(0xA9, instr.Opcode);
        Assert.Equal(2, instr.Cycles);
        Assert.Equal(0x42, instr.AAfter);
        Assert.False((instr.Flags & 0x02) != 0);  // Z flag should be clear
    }

    [Fact]
    public void Trace_CaptureLDAImmediateZero()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x00);  // LDA #$00
        Step();

        // Verify Z flag set for zero result
        var instr = trace.Instructions[0];
        Assert.Equal(0x00, instr.AAfter);
        Assert.True((instr.Flags & 0x02) != 0);  // Z flag should be set
    }

    [Fact]
    public void Trace_CaptureMultipleInstructions()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0xA2, 0x99);  // LDA #$42, LDX #$99
        Step(2);

        Assert.Equal(2, trace.Instructions.Count);
        Assert.Equal(0xA9, trace.Instructions[0].Opcode);
        Assert.Equal(0xA2, trace.Instructions[1].Opcode);
    }

    // ── Memory Access Tracing Tests ──────────────────────────────────────

    [Fact]
    public void Trace_CaptureMemoryRead()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA5, 0x80);  // LDA $80
        Ram.Write(0x80, 0x55);
        Step();

        // Should have instruction + fetch opcode + read operand address + read data
        // Fetch: PC=0x0200, opcode=0xA5
        // Operand fetch: PC=0x0201, value=0x80 (zero page address)
        // Data read: address=0x80, value=0x55
        var reads = trace.MemoryAccesses.Where(m => !m.IsWrite).ToList();
        Assert.True(reads.Count >= 3);  // At least: opcode fetch, address byte, data read

        // Find the final data read at 0x80
        var dataRead = reads.FirstOrDefault(m => m.Address == 0x80);
        Assert.NotNull(dataRead);
        Assert.Equal(0x55, dataRead.Value);
    }

    [Fact]
    public void Trace_CaptureMemoryWrite()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x77, 0x85, 0x80);  // LDA #$77, STA $80
        Step();  // LDA
        trace.Clear();  // Clear the LDA trace
        Step();  // STA

        // Should have memory write at 0x80 with value 0x77
        var writes = trace.MemoryAccesses.Where(m => m.IsWrite).ToList();
        Assert.Single(writes);
        Assert.Equal(0x80, writes[0].Address);
        Assert.Equal(0x77, writes[0].Value);
    }

    [Fact]
    public void Trace_CaptureLDAAbsolute()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xAD, 0x34, 0x12);  // LDA $1234
        Ram.Write(0x1234, 0xAB);
        Step();

        // Instruction should consume 4 cycles (no page cross)
        var instr = trace.Instructions[0];
        Assert.Equal(4, instr.Cycles);
        Assert.Equal(0xAB, instr.AAfter);
    }

    // ── Interrupt Tracing Tests ──────────────────────────────────────────

    [Fact]
    public void Trace_CaptureIRQ()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0x58, 0xEA, 0xEA);  // CLI, NOP, NOP
        Ram.Write(0xFFFE, 0x00);  // IRQ handler at 0x4000
        Ram.Write(0xFFFF, 0x40);

        // CLI to clear interrupt disable
        Step();
        Assert.False(Cpu.I);  // I flag should be clear
        
        // Request IRQ on next step
        Cpu.Irq();
        trace.Clear();
        Step();

        // Should have serviced IRQ
        Assert.Equal(0x4000, Cpu.PC);
        Assert.Single(trace.Interrupts);
        var irqEvent = trace.Interrupts[0];
        Assert.Equal(InterruptType.Irq, irqEvent.Type);
        Assert.Equal(0x4000, irqEvent.HandlerAddress);
    }

    [Fact]
    public void Trace_CaptureNMI()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xEA, 0xEA);  // NOP, NOP
        Ram.Write(0xFFFA, 0x00);  // NMI handler at 0x5000
        Ram.Write(0xFFFB, 0x50);

        // Execute one instruction, then request NMI before next
        Step();
        Cpu.Nmi();
        Step();

        // Second step should service NMI
        var interrupts = trace.Interrupts;
        Assert.Single(interrupts);
        var nmiEvent = interrupts[0];
        Assert.Equal(InterruptType.Nmi, nmiEvent.Type);
        Assert.Equal(0x5000, nmiEvent.HandlerAddress);
    }

    [Fact]
    public void Trace_CaptureNMIOverridesInterruptDisable()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0x78, 0xEA);  // SEI, NOP
        Ram.Write(0xFFFA, 0x00);  // NMI handler at 0x5000
        Ram.Write(0xFFFB, 0x50);

        // Execute SEI (sets I flag), then request NMI
        Step();
        Assert.True(Cpu.I);  // I flag should be set

        Cpu.Nmi();
        trace.Clear();
        Step();

        // NMI should still be serviced despite I=1
        var interrupts = trace.Interrupts;
        Assert.Single(interrupts);
        Assert.Equal(InterruptType.Nmi, interrupts[0].Type);
    }

    // ── Trace Utility Tests ──────────────────────────────────────────────

    [Fact]
    public void Trace_CanClear()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42);  // LDA #$42
        Step();

        Assert.Single(trace.Instructions);
        trace.Clear();
        Assert.Empty(trace.Instructions);
        Assert.Empty(trace.MemoryAccesses);
        Assert.Empty(trace.Interrupts);
    }

    [Fact]
    public void Trace_CanExportJson()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42);  // LDA #$42
        Step();

        string json = trace.ExportJson();
        Assert.NotEmpty(json);
        Assert.Contains("Instructions", json);
        Assert.Contains("512", json);  // 0x0200 = 512 decimal
    }

    // ── Performance Sanity Test ──────────────────────────────────────────

    [Fact]
    public void Trace_NullTraceHasMinimalCost()
    {
        // Execute same code with NullTrace and verify no exception
        // The key point is that tracing doesn't break execution

        Load(0x0200,
            0xA9, 0x42,  // LDA #$42
            0xA2, 0x99,  // LDX #$99
            0xA0, 0x77   // LDY #$77
        );

        ulong before = Cpu.TotalCycles;
        Step(3);
        ulong after = Cpu.TotalCycles;
        ulong delta = after - before;

        // Should be 2+2+2 = 6 cycles for 3 immediate loads
        Assert.Equal(6UL, delta);
    }
}
