# Opcode Dispatch Optimization Options

## Current State: Cpu6502

**Design:** `Action[] _ops` dispatch table + partial classes
- **File:** `src/Cpu6502.Core/Cpu.cs` line 31: `private readonly Action[] _ops = new Action[256];`
- **Step() implementation** (line 73-97): Fetches opcode, calls `_ops[opcode]()`
- **BuildDispatchTable()**: Wires 256 action delegates across partial class files
- **Behavior:** Each instruction is a separate method registered in the array

**Characteristics:**
- ✅ Clean separation: instruction families in separate files
- ✅ Maintainable: clear 1:1 mapping between partial file and instruction family
- ✅ Flexible: easy to extend with custom traces/validators
- ❌ Performance: delegate invocation cost per instruction (~1-3% overhead in tight loops)
- ❌ JIT unfriendly: can't inline across delegate boundaries; JIT doesn't generate jump tables

---

## Reference: CpuZ80 Sister Project

**Design:** Generated code + switch dispatch
- **Generator:** `src/CpuZ80.CodeGen/Program.cs` (declarative instruction table)
- **Generated:** `src/CpuZ80.Core/Cpu.Generated.cs` (~1000+ lines of switch cases)
- **Step() implementation:** Calls `StepGenerated(opcode)` with explicit switch
- **Behavior:** All 256 opcodes expanded inline as switch cases

**Characteristics:**
- ✅ **Zero delegate overhead:** Direct JIT-generated branch prediction + jump table
- ✅ **Inlining:** JIT can inline small instruction bodies into the switch
- ✅ **Cycle tracking:** Explicit `Tick()` calls in each case make timing auditable
- ✅ **Declarative:** Instruction definitions live in one config table (metadata-driven)
- ❌ **Binary size:** Generated file is ~1000 lines; .exe is larger
- ❌ **Maintenance:** Changes to instruction logic require regeneration
- ❌ **Not composable:** Can't reuse partial class organization (monolithic switch)

---

## Three Options for Cpu6502

### Option A: Keep Current (No Change)
**Trade-off:** Simplicity vs. Perf

- Keep `Action[]` delegates as-is
- **Pro:** No refactoring, maintainability + organization preserved
- **Con:** Leaves ~1-3% dispatch overhead on the table (acceptable for now)
- **When to use:** If real-world emulation speed is sufficient; revisit later if profiling shows dispatch is a bottleneck

---

### Option B: Switch-Based Dispatch (Like Z80)
**Trade-off:** Performance vs. Binary Size + Maintenance

Build a monolithic `switch (opcode)` in `Step()` or a helper:

```csharp
private void ExecuteOpcode(byte opcode)
{
    switch (opcode)
    {
        case 0xA9: // LDA #imm
            A = Fetch();
            SetZN(A);
            TotalCycles += 2;
            break;
        case 0xAA: // TAX
            X = A;
            SetZN(X);
            TotalCycles += 2;
            break;
        // ... all 256 cases ...
    }
}

public void Step()
{
    // ... interrupt service ...
    byte opcode = Fetch();
    ExecuteOpcode(opcode);
}
```

**Pros:**
- JIT generates native jump table (no delegate overhead)
- Inlining possible for simple instructions
- Cycle logic remains explicit inline
- ~2-5% perf gain in tight loops

**Cons:**
- Large single file or generated code (breaks modularity; 6502 has partial class organization)
- Merge conflicts on every opcode change
- Harder to organize by instruction family
- No need for code generation (Z80 uses it for 1000+ Z80 opcodes; 6502 only 151+illegal)

**Effort:** 3-4 PRs (move all handlers into one switch, retarget dispatcher, test across families)

---

### Option C: Hybrid - Indexed Private Method Dispatch
**Trade-off:** Performance vs. Minimal Disruption

Keep partial class organization but use **method pointers** instead of lambdas:

```csharp
// In Cpu.cs
private delegate void OpcodeHandler();
private readonly OpcodeHandler[] _ops = new OpcodeHandler[256];

// Instead of:
// _ops[0xA9] = () => LDA_Immediate();
// Use direct method reference:
private void BuildDispatchTable()
{
    _ops[0xA9] = LDA_Immediate;  // Direct method reference, not lambda
    _ops[0xAA] = TAX;
    // ...
}
```

**Pros:**
- ✅ Keeps all methods in partial classes (same organization)
- ✅ Method references are cheaper than lambdas
- ✅ JIT-friendly: can inline method references in dispatch hot path
- ✅ Minimal code churn: only change delegate creation, not instruction bodies
- ❌ Still uses delegate array (not a direct jump table), but less overhead than closures

**Effort:** 1 PR (update BuildDispatchTable to use method references instead of lambdas)

**Perf Gain:** ~0.5-1.5% (smaller than Option B but significant for tight loops)

---

## Recommended Path

1. **Short term (now):** Use **Option C (Hybrid)** 
   - Easiest refactor; keeps code organization
   - Method references are ~30-50% cheaper than lambda closures
   - Measurable perf gain for low overhead
   - Low risk of regression

2. **Medium term (if profiling shows dispatch is a bottleneck):**
   - **Option B (Switch)** if we need max perf and don't mind larger binary
   - Or stay with Option C if it's "good enough"

3. **Long term:** Monitor real-world usage; revisit if emulation speed becomes a concern for larger machines (e.g., cycle-stealing video DMA)

---

## Benchmarking Plan

Before & after for each option:
```csharp
// Benchmark: 1M instruction loop (tight LDA #$42 loop)
// Measure: total cycles, dispatch overhead, JIT compilation time

[Benchmark]
public void Dispatch_Overhead_1MLoops()
{
    for (int i = 0; i < 1_000_000; i++)
        cpu.Step();  // LDA #$42 repeating
    // Expect: baseline ~2M cycles (2 per LDA × 1M)
    // Overhead: extra dispatch time
}
```

---

## Files Affected by Each Option

| Option | Files | Scope |
|--------|-------|-------|
| A (Keep) | None | No change |
| B (Switch) | Cpu.cs + all Cpu.*.cs | Large refactor; consolidate into Cpu.cs or new dispatcher |
| C (Hybrid) | Cpu.cs only | Minimal; only BuildDispatchTable() logic |

---

## Decision Matrix

| Factor | Option A | Option B | Option C |
|--------|----------|----------|----------|
| **Perf gain** | None | 2-5% | 0.5-1.5% |
| **Effort** | 0 | 3-4 PRs | 1 PR |
| **Code organization** | Preserved | Lost (monolithic) | Preserved |
| **Maintainability** | High | Lower | High |
| **Risk** | None | Medium (consolidation) | Low |
| **When to use** | Current speed OK | Max perf needed | Balanced |
