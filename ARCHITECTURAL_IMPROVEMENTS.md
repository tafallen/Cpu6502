# Cpu6502 Emulator: Architectural Improvement Opportunities

This document captures 5 concrete architectural improvements identified during a comprehensive codebase review. All improvements:
- **Preserve backward compatibility** (existing code continues to work)
- **Require no changes to public `IBus` semantics**
- **Strengthen the emulation foundation** without cosmetic refactoring
- **Improve correctness, robustness, or debuggability**

---

## 1. Unified Bus Error Handling & Open-Bus Semantics

**Category:** Error handling and validation  
**Scope:** Medium  
**Effort:** 3-5 PRs

### Problem

- `AddressDecoder` returns `0xFF` for unmapped reads; unmapped writes are silent. This is correct for open-bus behavior, but there's **no mechanism to distinguish intentional open-bus from misconfiguration**.
- `Ram`, `Rom`, and peripheral `IBus` implementations have **no validation** on address range violations.
  - `Ram.Read(ushort address)` directly indexes `_data[address]` with no bounds check.
  - If `Rom` is accidentally sized smaller than the mapped range, it crashes instead of gracefully returning open-bus.
  - Debugging address map misconfigurations is painful.

### Solution

Create an `IBusValidator` interface that devices can optionally implement:

```csharp
public interface IBusValidator : IBus
{
    void ValidateAddress(ushort address);
}
```

Update `Ram` and `Rom` to implement it and bounds-check addresses:

```csharp
public sealed class Ram : IBusValidator
{
    private readonly ushort _size;
    private readonly byte[] _data;
    
    public Ram(ushort size) { _size = size; _data = new byte[size]; }
    
    public void ValidateAddress(ushort address)
    {
        if (address >= _size)
            throw new InvalidOperationException(
                $"Ram access at 0x{address:X4} exceeds size 0x{_size:X4}");
    }
    
    public byte Read(ushort address)
    {
        ValidateAddress(address);
        return _data[address];
    }
}
```

Add `#if DEBUG` mode in `AddressDecoder.Read/Write()` that calls `ValidateAddress()` on devices that support it:

```csharp
public byte Read(ushort address)
{
#if DEBUG
    if (device is IBusValidator validator)
        validator.ValidateAddress(address);
#endif
    return device.Read(offset);
}
```

### Benefits

- **Catches address map errors at development time** instead of silent crashes in production emulation
- **Debugger-friendly error messages** ("Ram access at 0x8000 exceeds size 0x7000")
- **Sets up future per-chip tracing and breakpoints**
- **Enables automated machine configuration validation**

### Implementation Steps

1. Define `IBusValidator` interface in `Machines.Common`
2. Update `Cpu6502.Core.Ram` and `Cpu6502.Core.Rom` to implement it
3. Add optional validation calls in `AddressDecoder.Read/Write` behind `#if DEBUG`
4. Add tests validating bounds-check exceptions
5. Document in `CLAUDE.md` the validation pattern

---

## 2. Explicit Interrupt Edge-Detection API

**Category:** Timing and interrupt handling  
**Scope:** Small  
**Effort:** 1-2 PRs

### Problem

- The CPU has two simple `bool` fields (`_nmiPending`, `_irqPending`) that are set by `Irq()`/`Nmi()` calls and serviced in `Step()`.
- **Timing coupling issue:** Machines like `Vic20Machine` manually manage interrupt edge detection with `_irqWasActive` field because the 6522 VIA timer sets the IRQ line level-high continuously, but the 6502 requires edge-triggered semantics on level-sensitive hardware.
- **No abstraction:** Each machine reimplements this pattern; there's no reusable interrupt controller.
- **Future bug risk:** Hard to discover that edge semantics are required for proper IRQ timing.

### Solution

Create an `InterruptEdgeDetector` in `Machines.Common`:

```csharp
public sealed class InterruptEdgeDetector
{
    private bool _lineWasActive;
    
    /// <summary>
    /// Detect a rising edge on the interrupt line.
    /// Returns true when line transitions from inactive to active.
    /// </summary>
    public bool Detect(bool lineActive)
    {
        bool edge = lineActive && !_lineWasActive;
        _lineWasActive = lineActive;
        return edge;
    }
}
```

Update `Vic20Machine` to use it instead of the `_irqWasActive` field:

```csharp
private readonly InterruptEdgeDetector _irqEdge = new();

// In machine timing loop
if (_irqEdge.Detect(Via1.Irq))
    Cpu.Irq();
```

Apply the same pattern to `AtomMachine` for consistency.

### Benefits

- **Reusable component:** Eliminates duplicate edge-detection logic across machines
- **One source of truth for interrupt timing:** Makes edge-semantics requirement explicit
- **Enables test fixtures:** Validate edge-detection semantics independently
- **Documents the timing requirement:** Future maintainers understand why edge detection exists

### Implementation Steps

1. Create `InterruptEdgeDetector` in `src/Machines.Common/`
2. Add unit tests in `tests/Machines.Atom.Tests/InterruptEdgeDetectorTests.cs`
3. Refactor `Vic20Machine` to use `InterruptEdgeDetector` instead of `_irqWasActive`
4. Refactor `AtomMachine` similarly if it has hidden edge-detection logic
5. Update `CLAUDE.md` to document level-sensitive IRQ with edge detection

---

## 3. Cycle Accounting Metadata Table

**Category:** Cycle accuracy and correctness  
**Scope:** Medium  
**Effort:** 3-4 PRs

### Problem

- **Scattered cycle logic:** Page-cross penalties are embedded inline in addressing mode methods (`AddrAbsoluteX()` / `AddrAbsoluteY()` add `+1` conditionally). Base cycles are hardcoded in each instruction (e.g., `TotalCycles += 4`).
- **Duplication:** The pattern `ReadByte(AddrAbsoluteX())` with `TotalCycles += 4` repeats 40+ times across load/store/logic/compare files.
- **Subtlety:** The correct total for writes (`STA abs,X`) is `5` (not `4 + page-cross`), but for reads (`LDA abs,X`) it's `4` or `5` depending on page cross. RMW instructions have different penalties again.
- **Hard to audit:** Verifying cycle correctness requires reading every instruction method. A false `+=4` where `+=5` is correct is a subtle bug.

### Solution

Create a cycle metadata table that makes timing properties explicit:

```csharp
public enum AddressingMode
{
    Immediate,
    ZeroPage,
    ZeroPageX,
    Absolute,
    AbsoluteX,
    AbsoluteY,
    // ... etc
}

public readonly record struct CycleInfo(int BaseCycles, bool PageCrossPenalty);

private static readonly Dictionary<(AddressingMode, AccessType), CycleInfo> CycleTable = new()
{
    // Reads
    { (AddressingMode.Immediate, AccessType.Read),   new(2, false) },
    { (AddressingMode.ZeroPage, AccessType.Read),    new(3, false) },
    { (AddressingMode.ZeroPageX, AccessType.Read),   new(4, false) },
    { (AddressingMode.Absolute, AccessType.Read),    new(4, false) },
    { (AddressingMode.AbsoluteX, AccessType.Read),   new(4, true) },   // +1 on page cross
    { (AddressingMode.AbsoluteY, AccessType.Read),   new(4, true) },   // +1 on page cross
    
    // Writes (no page-cross penalty)
    { (AddressingMode.ZeroPage, AccessType.Write),   new(3, false) },
    { (AddressingMode.Absolute, AccessType.Write),   new(4, false) },
    { (AddressingMode.AbsoluteX, AccessType.Write),  new(5, false) },  // always 5, no penalty
    { (AddressingMode.AbsoluteY, AccessType.Write),  new(5, false) },  // always 5, no penalty
    
    // RMW (Read-Modify-Write: always include page cross overhead in base)
    { (AddressingMode.ZeroPage, AccessType.Rmw),     new(5, false) },
    { (AddressingMode.Absolute, AccessType.Rmw),     new(6, false) },
    { (AddressingMode.AbsoluteX, AccessType.Rmw),    new(7, false) },  // always 7
    { (AddressingMode.AbsoluteY, AccessType.Rmw),    new(7, false) },  // always 7
};
```

Refactor instruction implementations to use a helper:

```csharp
private byte ReadWithCycles(ushort address, AddressingMode mode)
{
    var (baseCycles, hasPageCrossPenalty) = CycleTable[(mode, AccessType.Read)];
    TotalCycles += baseCycles;
    
    // Add page-cross penalty if applicable
    if (hasPageCrossPenalty && PageCrossed)
        TotalCycles += 1;
    
    return _bus.Read(address);
}

private void WriteWithCycles(ushort address, byte value, AddressingMode mode)
{
    var (baseCycles, _) = CycleTable[(mode, AccessType.Write)];
    TotalCycles += baseCycles;
    _bus.Write(address, value);
}
```

Simplify instruction methods like `DoLDA`:

```csharp
private void DoLDA(ushort address, AddressingMode mode)
{
    A = ReadWithCycles(address, mode);
    SetZ(A == 0);
    SetN((A & 0x80) != 0);
}
```

### Benefits

- **Eliminates cycle-count bugs:** All timing properties are explicit and centralized
- **Makes instruction set timing transparent:** Easy to audit correctness against [MCS6502 reference](http://archive.6502.org/)
- **Easier validation:** Unit tests can check cycle counts against golden reference
- **Sets up future profiling:** Enables per-chip cycle accounting (e.g., "LDA abs,Y accounts for 5M of the 20M total cycles this frame")
- **Reduces code duplication:** Instruction methods become shorter and simpler

### Implementation Steps

1. Define `AddressingMode` enum and `CycleInfo` record in a new `Cpu.CycleMetadata.cs` file
2. Build the comprehensive cycle table with all addressing modes and access types
3. Create `ReadWithCycles` and `WriteWithCycles` helpers
4. Refactor one instruction family (e.g., Load/Store) as a reference implementation
5. Apply the pattern to remaining instruction files
6. Add unit tests validating cycle counts against [Klaus Dörmann's CPU test data](https://github.com/Klaus2m5/6502_65C02_functional_tests)
7. Document in `CLAUDE.md` and code comments

---

## 4. Component Lifecycle & Initialization Validation

**Category:** Robustness and configuration  
**Scope:** Small  
**Effort:** 1-2 PRs

### Problem

- **Fragile initialization order:** `AtomMachine` and `Vic20Machine` constructors wire up delegates (e.g., `Ppi.ReadPortB = () => kb.ScanColumns(...)`), but there's no guarantee that dependencies are initialized before use.
- **Silent failures on null:** If a delegate is not wired up, it returns `0xFF` (defaults in `Via6522`, `Ppi8255`). A test that forgets to wire a keyboard adapter will silently read all-ones instead of failing.
- **No initialization marker:** There's no way to assert that a machine is properly "ready" before calling `Step()` or `RunFrame()`.

### Solution

Add an `IComponent` interface and machine validation:

```csharp
public interface IComponent
{
    /// <summary>
    /// Validate that all required dependencies are initialized.
    /// Throws InvalidOperationException if validation fails.
    /// </summary>
    void ValidateInitialization();
}

public sealed class AtomMachine : IComponent
{
    public void ValidateInitialization()
    {
        if (Ppi.ReadPortB == null)
            throw new InvalidOperationException(
                "Keyboard adapter not wired to PPI.ReadPortB. " +
                "Machine is not ready to execute. Set keyboard parameter in constructor.");
        
        if (Cpu == null)
            throw new InvalidOperationException("CPU not initialized");
        
        // Validate other critical wiring
        if (VideoRam == null || VideoRam.RawBytes.Length == 0)
            throw new InvalidOperationException("Video RAM not initialized");
    }
}
```

Call validation during machine construction or provide a factory method:

```csharp
public static AtomMachine Create(
    byte[] basicRom,
    byte[] osRom,
    IPhysicalKeyboard keyboard,
    IAudioSink audioSink,
    byte[]? floatRom = null,
    byte[]? dosRom = null,
    byte[]? extRom = null,
    byte[]? charRom = null,
    AtomTapeAdapter? tape = null)
{
    var machine = new AtomMachine(basicRom, osRom, keyboard, audioSink, floatRom, dosRom, extRom, charRom, tape);
    machine.ValidateInitialization();
    return machine;
}
```

Alternatively, call `ValidateInitialization()` in the constructor after all wiring is complete.

### Benefits

- **Catches wiring mistakes at startup, not during emulation:** Fail fast with clear error messages
- **Enables machine configuration schemas:** Declarative address maps and dependency graphs
- **Reduces debugging friction:** Test failures immediately surface configuration errors
- **Documents required dependencies:** Code is self-documenting about what must be wired

### Implementation Steps

1. Define `IComponent` interface in `Machines.Common`
2. Implement `IComponent.ValidateInitialization()` in `AtomMachine` and `Vic20Machine`
3. Call validation at the end of each machine constructor (or via factory)
4. Add unit tests that verify validation catches missing adapters
5. Update machine constructors' XML comments to list all required dependencies
6. Document in `CLAUDE.md` the initialization validation pattern

---

## 5. Debugger-Ready Trace Abstraction

**Category:** Debugging and profiling  
**Scope:** Medium-Large  
**Effort:** 4-6 PRs

### Problem

- **Hard to debug:** There's no built-in way to trace execution. Current options: 1) manually instrument the CPU with `Console.WriteLine()`, 2) step through with a debugger (slow for real workloads), or 3) write a test that validates end state (black-box).
- **No instruction history:** Users can't answer questions like "what sequence of instructions led to PC=0x1234?" or "which instruction wrote to address 0x5000?"
- **Cycle provenance unclear:** The `OnCyclesConsumed` callback tells you cycles consumed, but not *which instruction consumed them* or *why* (page-cross penalty, interrupt, etc.).
- **VIA/PPI timing opaque:** Peripherals have no way to log state transitions (e.g., when IFR flags are set, when timers fire).

### Solution

Create a trace abstraction that machines can optionally use:

```csharp
public enum InterruptType { Irq, Nmi, BrkOpcode }

public interface IExecutionTrace
{
    void OnInstructionFetched(ushort pc, byte opcode);
    void OnInstructionExecuted(ushort pc, byte opcode, int cycles, byte aAfter, byte flags);
    void OnMemoryAccess(ushort address, byte value, bool isWrite);
    void OnInterrupt(InterruptType type, ushort handlerAddress);
}

public sealed class NullTrace : IExecutionTrace
{
    public static readonly IExecutionTrace Instance = new();
    
    public void OnInstructionFetched(ushort pc, byte opcode) { }
    public void OnInstructionExecuted(ushort pc, byte opcode, int cycles, byte aAfter, byte flags) { }
    public void OnMemoryAccess(ushort address, byte value, bool isWrite) { }
    public void OnInterrupt(InterruptType type, ushort handlerAddress) { }
}
```

Inject into `Cpu`:

```csharp
public sealed partial class Cpu
{
    private IExecutionTrace _trace = NullTrace.Instance;
    
    public IExecutionTrace Trace 
    { 
        get => _trace; 
        set => _trace = value ?? NullTrace.Instance; 
    }
    
    public void Step()
    {
        ushort pcBefore = PC;
        byte opBefore = Fetch();
        PC--;  // peek opcode to trace before increment
        
        _trace.OnInstructionFetched(pcBefore, opBefore);
        
        // ... execute instruction ...
        
        ulong cyclesConsumed = TotalCycles - cyclesBefore;
        _trace.OnInstructionExecuted(pcBefore, opBefore, (int)cyclesConsumed, A, GetStatus());
    }
}
```

Provide a test implementation:

```csharp
public sealed class RecordingTrace : IExecutionTrace
{
    public sealed record InstructionEvent(ushort Pc, byte Opcode, int Cycles, byte A, byte Flags);
    public sealed record MemoryAccessEvent(ushort Address, byte Value, bool IsWrite);
    
    public List<InstructionEvent> Instructions { get; } = new();
    public List<MemoryAccessEvent> MemoryAccesses { get; } = new();
    
    public void OnInstructionFetched(ushort pc, byte opcode) { }
    
    public void OnInstructionExecuted(ushort pc, byte opcode, int cycles, byte aAfter, byte flags)
    {
        Instructions.Add(new(pc, opcode, cycles, aAfter, flags));
    }
    
    public void OnMemoryAccess(ushort address, byte value, bool isWrite)
    {
        MemoryAccesses.Add(new(address, value, isWrite));
    }
    
    public void OnInterrupt(InterruptType type, ushort handlerAddress) { }
}
```

Usage in tests:

```csharp
[Fact]
public void Trace_CaptureLDAImmediate()
{
    var cpu = new Cpu(ram);
    var trace = new RecordingTrace();
    cpu.Trace = trace;
    
    Load(0x0200, 0xA9, 0x42);  // LDA #$42
    cpu.Step();
    
    Assert.Single(trace.Instructions);
    Assert.Equal(0x0200, trace.Instructions[0].Pc);
    Assert.Equal(0xA9, trace.Instructions[0].Opcode);
    Assert.Equal(0x42, trace.Instructions[0].A);
    Assert.Equal(2, trace.Instructions[0].Cycles);
}
```

### Benefits

- **Unblocks debugging workflows:** Conditional breakpoints, instruction history, hotspot analysis
- **No core-emulator bloat:** Trace logic is orthogonal; disabled by default (zero-cost abstraction)
- **Integration with external tools:** JSON export for analysis scripts, GDB integration, etc.
- **Performance profiling:** Identify instruction-level hotspots
- **Regression debugging:** Record traces of working behavior for comparison

### Implementation Steps

1. Define `IExecutionTrace` and `NullTrace` in `Cpu6502.Core`
2. Add `Trace` property to `Cpu` class
3. Instrument `Cpu.Step()` to call trace callbacks (use `#if` guards if performance-sensitive)
4. Create `RecordingTrace` test implementation in `tests/Cpu6502.Tests/`
5. Add trace tests validating instruction/memory/interrupt events
6. Create optional JSON export method for trace analysis
7. Document trace usage in `CLAUDE.md` with examples
8. (Future) Add GDB protocol adapter or VS Code debugger integration

---

## Summary Table

| # | Area | Scope | Effort | Impact | Risk |
|---|------|-------|--------|--------|------|
| **1** | Bus error validation | Medium | Medium | High — catches config errors | Low — optional validation |
| **2** | Interrupt edge detection | Small | Small | Medium — reusable, clearer | Low — isolated component |
| **3** | Cycle metadata table | Medium | Medium-High | High — eliminates timing bugs | Medium — large refactor |
| **4** | Component lifecycle | Small | Small | Medium — clear init order | Low — simple assertions |
| **5** | Execution trace API | Medium-Large | Medium-High | High — unblocks debugging | Low — orthogonal feature |

---

## Recommended Implementation Order

1. **#2 (Interrupt edge detection)** — Quick win; isolated; enables future timing work
2. **#4 (Component lifecycle)** — Small; immediate friction relief; sets pattern
3. **#1 (Bus error handling)** — Medium effort; high confidence; catches real bugs
4. **#3 (Cycle metadata)** — Largest refactor; highest correctness impact; start after #1/#2
5. **#5 (Trace abstraction)** — Orthogonal; high value for debugging; good capstone

---

## Integration with Existing Architecture

All improvements **integrate cleanly** with the existing codebase:

- **IBusValidator** extends `IBus` (backward-compatible interface extension)
- **InterruptEdgeDetector** is a utility class (no architectural changes)
- **CycleMetadata** replaces hardcoded cycles (internal refactor, no API changes)
- **IComponent** is an optional interface (opt-in validation)
- **IExecutionTrace** is injected via property (zero-cost when unused)

No changes required to:
- Machine or CPU public APIs
- `Cpu.Step()` behavior
- Address decoder semantics
- Interrupt/timing logic (only refactored)

---

## Future Extensions

These improvements enable:

- **Per-chip cycle profiling:** Which instructions/addressing modes consume the most time per frame?
- **Memory access patterns:** Which addresses are hotly accessed? Opportunity for optimization?
- **Interrupt latency analysis:** How much time between IRQ assertion and handler execution?
- **Peripheral timing audits:** Are VIA timers firing at the right cycle boundaries?
- **GDB integration:** Step through emulated 6502 code in an external debugger
- **Regression testing:** Capture "golden traces" and compare against regressions

---

## References

- MOS Technology 6502 CPU: http://archive.6502.org/
- Klaus Dörmann 6502 Functional Tests: https://github.com/Klaus2m5/6502_65C02_functional_tests
- CLAUDE.md: Architecture and testing guidance
- Existing timing work: `src/Machines.Common/MachineClock.cs`, `src/Machines.Common/TimingScheduler.cs`
