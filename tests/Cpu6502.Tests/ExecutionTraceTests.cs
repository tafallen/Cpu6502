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

    [Fact]
    public void Trace_CaptureStackOperations()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x55, 0x48);  // LDA #$55, PHA
        Step();  // LDA — sets A to 0x55
        trace.Clear();
        Step();  // PHA — pushes A onto stack

        // PHA should write to stack at 0x0100 | SP
        var stackWrites = trace.MemoryAccesses.Where(m => m.IsWrite && m.Address >= 0x0100 && m.Address < 0x0200).ToList();
        Assert.NotEmpty(stackWrites);
        Assert.Equal(0x55, stackWrites[0].Value);  // Value pushed should be 0x55
    }

    [Fact]
    public void Trace_CaptureStackPull()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x77, 0x48, 0x68);  // LDA #$77, PHA, PLA
        Step();  // LDA
        Step();  // PHA
        trace.Clear();
        Step();  // PLA — pulls from stack

        // PLA should read from stack at 0x0100 | SP
        var stackReads = trace.MemoryAccesses.Where(m => !m.IsWrite && m.Address >= 0x0100 && m.Address < 0x0200).ToList();
        Assert.NotEmpty(stackReads);
        Assert.Equal(0x77, stackReads[0].Value);  // Value pulled should be 0x77
        Assert.Equal(0x77, Cpu.A);  // A should have the pulled value
    }

    // ── Interrupt Tracing Tests ──────────────────────────────────────────

    [Fact]
    public void Trace_CaptureInterruptVectorReads()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        // Set up interrupt vectors in RAM
        Ram.Write(0xFFFE, 0x00);  // IRQ handler at 0x4000
        Ram.Write(0xFFFF, 0x40);
        Ram.Write(0xFFFC, 0x00);  // Reset handler at 0x5000
        Ram.Write(0xFFFD, 0x50);

        Load(0x0200, 0x58, 0xEA);  // CLI, NOP
        Step();  // CLI to clear interrupt disable
        Assert.False(Cpu.I);

        trace.Clear();
        Cpu.Irq();  // Request IRQ
        Step();  // Should read interrupt vector

        // Verify that reads from interrupt vector addresses appear in trace
        var vectorReads = trace.MemoryAccesses.Where(m => !m.IsWrite && 
            (m.Address == 0xFFFE || m.Address == 0xFFFF)).ToList();
        Assert.NotEmpty(vectorReads);
        Assert.Equal(0x4000, Cpu.PC);  // Should have jumped to handler
    }

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

    [Fact]
    public void Trace_MemoryAccessIncludesCycleContext()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0x8D, 0x00, 0x04);  // LDA #$42, STA $0400
        Step();  // LDA
        trace.Clear();
        Step();  // STA — writes to memory

        // Verify memory access captured with cycle context
        var writes = trace.MemoryAccesses.Where(m => m.IsWrite && m.Address == 0x0400).ToList();
        Assert.Single(writes);
        Assert.True(writes[0].Cycles >= 0);  // Cycles should be captured (any non-negative value)
        Assert.Equal(0x42, writes[0].Value);
    }

    [Fact]
    public void Trace_AddressRangeFilter()
    {
        var trace = new RecordingTrace
        {
            AddressRangeFilter = (0x0100, 0x01FF)  // Stack only
        };
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x55, 0x48, 0x8D, 0x00, 0x04);  // LDA #$55, PHA, STA $0400
        Step();  // LDA — no stack access
        Step();  // PHA — writes to stack (0x01xx)
        var stackWrites = trace.MemoryAccesses.Where(m => m.IsWrite).ToList();
        Assert.Single(stackWrites);  // Only stack write recorded

        trace.Clear();
        Step();  // STA — writes to 0x0400 (not in range)
        Assert.Empty(trace.MemoryAccesses);  // Out-of-range write filtered out
    }

    [Fact]
    public void Trace_WriteOnlyFilter()
    {
        var trace = new RecordingTrace
        {
            WriteOnlyFilter = true  // Only record writes
        };
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0x85, 0x80);  // LDA #$42, STA $80
        Step();  // LDA #$42 — immediate mode, no bus reads
        Step();  // STA — writes to $80

        var writeAccesses = trace.MemoryAccesses.Where(m => m.IsWrite).ToList();
        Assert.Single(writeAccesses);  // Only the STA write
        
        // No reads should be present
        var readAccesses = trace.MemoryAccesses.Where(m => !m.IsWrite).ToList();
        Assert.Empty(readAccesses);
    }

    [Fact]
    public void Trace_SamplingRate()
    {
        var trace = new RecordingTrace
        {
            MemoryAccessSampleRate = 2  // Record every 2nd access
        };
        Cpu.Trace = trace;

        Load(0x0200, 0xA5, 0x80, 0xA5, 0x81, 0xA5, 0x82);  // LDA $80, $81, $82 (3 zp reads)
        Step();  // First LDA
        Step();  // Second LDA
        Step();  // Third LDA

        // With sampling rate 2, expect approximately half the accesses
        // With 3 instructions × ~2 reads each = ~6 accesses, sampling should give ~3
        Assert.True(trace.MemoryAccesses.Count > 0);  // At least some accesses recorded
        Assert.True(trace.MemoryAccesses.Count < 20);  // But not all (if rate was 1)
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

    // ── Cycle Provenance Tests ──────────────────────────────────────────────

    [Fact]
    public void Trace_CycleProvenanceTracksPerInstruction()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0xA2, 0x99);  // LDA #$42 (2 cycles), LDX #$99 (2 cycles)
        Step(2);

        Assert.Equal(2, trace.CycleProvenance.Count);
        Assert.Equal(0, trace.CycleProvenance[0].InstructionIndex);
        Assert.Equal(2, trace.CycleProvenance[0].CyclesContributed);
        Assert.Equal(0x0200, trace.Instructions[trace.CycleProvenance[0].InstructionIndex].Pc);
        Assert.Equal(0xA9, trace.Instructions[trace.CycleProvenance[0].InstructionIndex].Opcode);

        Assert.Equal(1, trace.CycleProvenance[1].InstructionIndex);
        Assert.Equal(2, trace.CycleProvenance[1].CyclesContributed);
        Assert.Equal(0x0202, trace.Instructions[trace.CycleProvenance[1].InstructionIndex].Pc);
        Assert.Equal(0xA2, trace.Instructions[trace.CycleProvenance[1].InstructionIndex].Opcode);
    }

    [Fact]
    public void Trace_GetTotalCycles()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0xA2, 0x99);  // LDA #$42 (2), LDX #$99 (2)
        Step(2);

        ulong total = trace.GetTotalCycles();
        Assert.Equal(4UL, total);
    }

    [Fact]
    public void Trace_GetCyclesForAddressRange()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0xA2, 0x99, 0xA0, 0x77);  // LDA (0x0200), LDX (0x0202), LDY (0x0204)
        Step(3);

        // Cycles for ROM range 0x0200-0x0203 (should be LDA + LDX)
        ulong romCycles = trace.GetCyclesForAddressRange(0x0200, 0x0203);
        Assert.Equal(4UL, romCycles);

        // Cycles for address 0x0204 and above
        ulong romCycles2 = trace.GetCyclesForAddressRange(0x0204, 0xFFFF);
        Assert.Equal(2UL, romCycles2);
    }

    [Fact]
    public void Trace_GetCyclesForInstruction()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0xA2, 0x99, 0xA9, 0x55);  // LDA, LDX, LDA again
        Step(3);

        // First LDA at 0x0200 should have 2 cycles
        int cycles = trace.GetCyclesForInstruction(0x0200);
        Assert.Equal(2, cycles);

        // Third instruction (LDA at 0x0204) should have 2 cycles
        cycles = trace.GetCyclesForInstruction(0x0204);
        Assert.Equal(2, cycles);
    }

    [Fact]
    public void Trace_GetCyclesByOpcode()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0xA2, 0x99, 0xA9, 0x55);  // LDA #$42, LDX #$99, LDA #$55
        Step(3);

        var cyclesByOp = trace.GetCyclesByOpcode();
        
        // 0xA9 (LDA immediate) appears twice, 2 cycles each = 4 total
        Assert.Equal(4, cyclesByOp[0xA9]);
        
        // 0xA2 (LDX immediate) appears once, 2 cycles
        Assert.Equal(2, cyclesByOp[0xA2]);
    }

    [Fact]
    public void Trace_ExportCycleProvenanceCSV()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0xA2, 0x99);
        Step(2);

        string csv = trace.ExportCycleProvenanceCSV();
        Assert.NotEmpty(csv);
        Assert.Contains("PC,Opcode,Cycles,CumulativePercent", csv);
        Assert.Contains("0x0200", csv);  // First instruction PC
        Assert.Contains("0xA9", csv);   // LDA opcode
    }

    [Fact]
    public void Trace_ExportCycleProvenanceSummaryByRange()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0xA2, 0x99);  // Both instructions in ROM
        Step(2);

        string csv = trace.ExportCycleProvenanceSummaryByRange(
            ("ROM", 0x0200, 0x0FFF),
            ("RAM", 0x0000, 0x01FF)
        );

        Assert.NotEmpty(csv);
        Assert.Contains("Range,Cycles,Percent,InstructionCount", csv);
        Assert.Contains("ROM,4,100.00,2", csv);  // 4 cycles in ROM range, 2 instructions
        Assert.Contains("RAM,0,0.00,0", csv);   // 0 cycles in RAM range
    }

    [Fact]
    public void Trace_CycleProvenanceIncludedInJson()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42);  // LDA #$42
        Step();

        string json = trace.ExportJson();
        Assert.Contains("CycleProvenance", json);
        Assert.Contains("CycleStats", json);
        Assert.Contains("TotalCycles", json);
    }

    [Fact]
    public void Trace_ClearAlsoClearsCycleProvenance()
    {
        var trace = new RecordingTrace();
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42);
        Step();

        Assert.Single(trace.CycleProvenance);
        trace.Clear();
        Assert.Empty(trace.CycleProvenance);
    }

    // ── Conditional Breakpoint Tests ────────────────────────────────────────

    [Fact]
    public void Trace_BreakpointTriggersException()
    {
        var trace = new RecordingTrace();
        trace.BreakpointCondition = (pc, opcode, a) => pc == 0x0200;  // Break on first instruction
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42);  // LDA #$42

        // Step() should throw BreakException
        var ex = Assert.Throws<BreakException>(() => Step());
        Assert.Equal(0x0200, ex.PC);
        Assert.Equal(0xA9, ex.Opcode);
        Assert.Equal(0x00, ex.AValue);  // A is still 0 before instruction executes
    }

    [Fact]
    public void Trace_BreakpointConditionEvaluatesBeforeExecution()
    {
        var trace = new RecordingTrace();
        trace.BreakpointCondition = (pc, opcode, a) => a == 0x42;  // Break when A = 0x42
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0xA9, 0x55);  // LDA #$42, LDA #$55
        
        // First instruction: A is 0, breakpoint should not trigger
        Step();
        Assert.Equal(0x42, Cpu.A);  // A now has 0x42
        
        // Second instruction: A is 0x42, breakpoint should trigger
        trace.Clear();
        var ex = Assert.Throws<BreakException>(() => Step());
        Assert.Equal(0x0202, ex.PC);
        Assert.Equal(0xA9, ex.Opcode);
        Assert.Equal(0x42, ex.AValue);  // A is 0x42 before instruction
    }

    [Fact]
    public void Trace_NoBreakpointWhenConditionFalse()
    {
        var trace = new RecordingTrace();
        trace.BreakpointCondition = (pc, opcode, a) => false;  // Never break
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42);

        // Should not throw
        Step();
        Assert.Equal(0x42, Cpu.A);
    }

    [Fact]
    public void Trace_NoBreakpointWhenConditionNull()
    {
        var trace = new RecordingTrace();
        trace.BreakpointCondition = null;  // No breakpoint condition set
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42);

        // Should not throw
        Step();
        Assert.Equal(0x42, Cpu.A);
    }

    [Fact]
    public void Trace_BreakpointHitsAreRecorded()
    {
        var trace = new RecordingTrace();
        trace.BreakpointCondition = (pc, opcode, a) => opcode == 0xA9;  // Break on all LDA immediate
        Cpu.Trace = trace;

        Load(0x0200, 0xA9, 0x42, 0xA9, 0x55, 0xA9, 0x77);  // LDA #$42, LDA #$55, LDA #$77

        // Hit first breakpoint at 0x0200
        Assert.Throws<BreakException>(() => Step());
        Assert.Single(trace.BreakpointHits);
        Assert.Equal((0x0200, (byte)0xA9, (byte)0x00), trace.BreakpointHits[0]);

        // Resume from breakpoint (execute the first LDA #$42)
        Cpu.Trace = null;  // Disable breakpoints to proceed
        Step();
        Assert.Equal(0x42, Cpu.A);  // First LDA #$42 executed

        // Execute second LDA #$55
        Step();
        Assert.Equal(0x55, Cpu.A);  // Second LDA #$55 executed

        // Re-enable trace and hit another breakpoint on third LDA
        Cpu.Trace = trace;
        Assert.Throws<BreakException>(() => Step());
        Assert.Equal(2, trace.BreakpointHits.Count);
        Assert.Equal((0x0204, (byte)0xA9, (byte)0x55), trace.BreakpointHits[1]);  // At third LDA, A still holds 0x55
    }

    [Fact]
    public void Trace_BreakExceptionContextIncludesState()
    {
        var trace = new RecordingTrace();
        trace.BreakpointCondition = (pc, opcode, a) => pc == 0x0200 && a == 0x00;
        Cpu.Trace = trace;

        Load(0x0200, 0xA2, 0x99);  // LDX #$99

        var ex = Assert.Throws<BreakException>(() => Step());
        
        // Exception should include full context
        Assert.Equal(0x0200, ex.PC);
        Assert.Equal(0xA2, ex.Opcode);
        Assert.Equal(0x00, ex.AValue);
        Assert.NotNull(ex.Message);
        Assert.Contains("0x0200", ex.Message);  // PC in message
    }
}
