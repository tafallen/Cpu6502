# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build everything
dotnet build

# Run all tests (includes coverage enforcement — fails if line coverage < 80%)
dotnet test

# Run tests for a specific project
dotnet test tests/Cpu6502.Tests/
dotnet test tests/Machines.Atom.Tests/

# Run a single test class
dotnet test --filter "ClassName=Cpu6502.Tests.ArithmeticTests"
dotnet test --filter "ClassName=Machines.Atom.Tests.Ppi8255Tests"

# Run a single test method
dotnet test --filter "FullyQualifiedName=Cpu6502.Tests.ArithmeticTests.ADC_Immediate_AddsWithoutCarry"

# Run tests without coverage (faster during development)
dotnet test /p:CollectCoverage=false

# Run the Atom emulator
dotnet run --project src/Host.Atom -- --basic atom-basic.rom --os atom-os.rom
dotnet run --project src/Host.Atom -- --basic atom-basic.rom --os atom-os.rom --tape game.uef
dotnet run --project src/Host.Atom -- --basic atom-basic.rom --os atom-os.rom --smooth --scanlines 0.5

# Run the VIC-20 emulator
dotnet run --project src/Host.Vic20 -- --basic basic.rom --kernal kernal.rom
dotnet run --project src/Host.Vic20 -- --basic basic.rom --kernal kernal.rom --smooth --scanlines 0.3
```

Coverage reports are written to `tests/*/coverage/coverage.cobertura.xml` after each `dotnet test` run.

## Repository layout

```
src/
  Cpu6502.Core/       — 6502 CPU, Ram, Rom, AddressDecoder (IBus primitives)
  Machines.Common/    — Shared interfaces: IBus, IVideoSink, IAudioSink, IPhysicalKeyboard, ITapeDevice; components: InterruptEdgeDetector, IComponent, MachineClock, TimingScheduler
  Machines.Atom/      — Acorn Atom hardware emulation
  Machines.Vic20/     — Commodore VIC-20 hardware emulation
  Adapters.Raylib/    — RaylibHost: window, input, audio (IVideoSink + IPhysicalKeyboard + IAudioSink)
  Host.Atom/          — Console entry point; CLI parsing (--basic, --os, --tape, --smooth, --scanlines, etc.)
  Host.Vic20/         — Console entry point; CLI parsing (--basic, --kernal, --smooth, --scanlines, etc.)
tests/
  Cpu6502.Tests/      — CPU unit tests + Klaus Dörmann integration test + RecordingTrace (conditional breakpoints, memory export)
  Machines.Atom.Tests/  — Atom hardware tests (Ppi8255, Mc6847, keyboard, sound, tape, UEF, machine initialization)
  Machines.Vic20.Tests/ — VIC-20 hardware tests (Via6522, VicI, keyboard, tape, machine initialization)
docs/
  walkthrough.md      — IBus / AddressDecoder / CPU tutorial
  atom.md             — Acorn Atom hardware reference: address map, chips, ROM layout, VBL timing, CLI options
  vic20.md            — Commodore VIC-20 hardware reference: address map, chips, ROM layout, CLI options
  vic20-tape.md       — VIC-20 TAP tape format details
  electron.md         — Acorn Electron hardware reference (not yet implemented): ULA, address map, ROM banking, video modes
```

## Architecture

The goal is to run real 80s ROM images by composing hardware components into machine definitions. The CPU knows nothing about the machine it is in — it only talks to `IBus`.

### Hardware composition pattern

```
Cpu  →  IBus
              AddressDecoder  (routes by address range)
                ├── Ram       ($0000–$7FFF)
                ├── Rom       ($8000–$BFFF)  ← load ROM image bytes here
                └── Peripheral (IBus)        ← PPI, ULA, VIA, etc.
```

`AddressDecoder.Map(from, to, device)` registers a component for an inclusive address range. The decoder passes `(address - from)` to the device, so devices always see a zero-based offset. Last registration wins on overlap. Unmapped reads return `0xFF` (open bus); unmapped writes are silent.

### CPU implementation

`Cpu` is a `sealed partial` class split across files by instruction group:

| File | Content |
|---|---|
| `Cpu.cs` | Registers, flags, `Reset`/`Step`/`Irq`/`Nmi`, status byte helpers, dispatch table builder |
| `Cpu.CycleMetadata.cs` | Cycle timing table: 11 addressing modes × 3 access types → base cycles + page-cross penalty flag |
| `Cpu.AddressingModes.cs` | All 11 addressing modes as `private` methods returning `ushort` effective address |
| `Cpu.LoadStore.cs` | LDA/LDX/LDY/STA/STX/STY |
| `Cpu.Arithmetic.cs` | ADC/SBC/INC/DEC/INX/INY/DEX/DEY (includes BCD mode) |
| `Cpu.Logic.cs` | AND/ORA/EOR/BIT |
| `Cpu.Shifts.cs` | ASL/LSR/ROL/ROR |
| `Cpu.Compare.cs` | CMP/CPX/CPY |
| `Cpu.Branches.cs` | BCC/BCS/BEQ/BNE/BMI/BPL/BVC/BVS |
| `Cpu.Jumps.cs` | JMP/JSR/RTS/BRK/RTI |
| `Cpu.Transfer.cs` | TAX/TAY/TXA/TYA/TSX/TXS/PHA/PLA/PHP/PLP |
| `Cpu.Flags.cs` | CLC/SEC/CLI/SEI/CLD/SED/CLV/NOP |
| `Cpu.Illegal.cs` | Undocumented NMOS 6502 opcodes (LAX, SAX, DCP, ISB, etc.) |

Opcodes are dispatched via `Action[] _ops` (256 slots). Unimplemented opcodes throw `InvalidOperationException`. All cycle accounting uses `GetCycleInfo(AddressingMode, AccessType)` lookups from the central cycle table.

### Undocumented (illegal) NMOS 6502 opcodes

The full NMOS 6502 undocumented opcode set is implemented in `Cpu.Illegal.cs` and registered in `BuildDispatchTable()`. These were added when the VIC-20 kernal was found to use several of them. **Do not remove or stub them out** — real machines rely on them.

| Mnemonic | Opcodes | Description |
|---|---|---|
| LAX | `$A3 $A7 $AF $B3 $B7 $BF` | LDA + LDX same value |
| SAX | `$83 $87 $8F $97` | Store A & X (no flags) |
| DCP | `$C3 $C7 $CF $D3 $D7 $DB $DF` | DEC memory then CMP A |
| ISB/ISC | `$E3 $E7 $EF $F3 $F7 $FB $FF` | INC memory then SBC A |
| SLO | `$03 $07 $0F $13 $17 $1B $1F` | ASL memory then ORA A |
| RLA | `$23 $27 $2F $33 $37 $3B $3F` | ROL memory then AND A |
| SRE | `$43 $47 $4F $53 $57 $5B $5F` | LSR memory then EOR A |
| RRA | `$63 $67 $6F $73 $77 $7B $7F` | ROR memory then ADC A |
| ANC | `$0B $2B` | AND imm; C = bit 7 of result |
| ALR | `$4B` | AND imm then LSR accumulator |
| ARR | `$6B` | AND imm then ROR (complex V/C flags) |
| SBX/AXS | `$CB` | (A & X) − imm → X; sets C/Z/N |
| USBC | `$EB` | SBC immediate (duplicate of `$E9`) |
| SHX | `$9E` | Store X & (addr_hi + 1), abs,Y |
| SHY | `$9C` | Store Y & (addr_hi + 1), abs,X |
| SHA | `$9F $93` | Store A & X & (addr_hi + 1) |
| TAS/XAS | `$9B` | SP = A & X; store A & X & (addr_hi + 1) abs,Y |
| LAS/LAR | `$BB` | (SP & mem) → A, X, SP; abs,Y |
| XAA/ANE | `$8B` | Unstable; emulated as (A \| $EE) & X & imm |
| LXA/OAL | `$AB` | Unstable; emulated as (A \| $EE) & imm → A, X |
| NOP variants | many | Various implied/immediate/zp/abs NOPs |

KIL/JAM opcodes (`$02 $12 $22 $32 $42 $52 $62 $72 $92 $B2 $D2 $F2`) are **not** implemented; they throw `InvalidOperationException` because hitting one always indicates the CPU has gone off the rails.

### Cycle accounting metadata table

Cycle timing is centralized in `Cpu.CycleMetadata.cs` via a static `CycleTable` dictionary. This eliminates scattered hardcoded cycle counts and makes timing properties auditable:

**Cycle table structure:**
- Key: `(AddressingMode, AccessType)` tuple
- Value: `CycleInfo` record with `BaseCycles` and `PageCrossPenalty` flag
- 11 addressing modes × 3 access types = 33 table entries covering all instruction patterns

**Addressing modes:** Immediate, ZeroPage, ZeroPageX, ZeroPageY, Absolute, AbsoluteX, AbsoluteY, IndirectX, IndirectY, Indirect, Relative

**Access types:**
- **Read**: Reads data from memory; page-cross penalty applies on read (e.g., `LDA $1234,X`)
- **Write**: Writes data to memory; no page-cross penalty (cost baked into base)
- **Rmw**: Read-Modify-Write (e.g., `INC $1234,X`); no page-cross penalty (cost baked into base)

**Lookup pattern:**

```csharp
// In Load/Store/Arithmetic/Logic/Compare/Shifts instruction methods:
private void LDA_AbsX()
{
    A = ReadByte(AddrAbsoluteX());
    TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Read).BaseCycles;
}
```

The `GetCycleInfo()` method returns the cycle metadata; the addressing mode helper (`AddrAbsoluteX()`) already adds the page-cross penalty inline if needed, so the returned base cycles include any conditional additions.

**Cycle table design principles:**

1. **Immediate & ZeroPage modes:** No page-cross penalty possible; `PageCrossPenalty = false`
2. **Absolute,X / Absolute,Y reads:** `PageCrossPenalty = true`; base = 4, +1 if page cross
3. **Absolute,X / Absolute,Y writes:** `PageCrossPenalty = false`; base = 5 (includes page-cross overhead)
4. **RMW instructions:** `PageCrossPenalty = false`; base includes full cost (6 for ZeroPage, 7 for AbsoluteX)
5. **Branch instructions (Relative):** `PageCrossPenalty = true`; base = 2, +1 if taken, +1 if page cross on branch
6. **Indirect indexed reads:** `PageCrossPenalty = true` for IndirectY, false for IndirectX

**Adding a custom instruction with cycle metadata:**

```csharp
// New instruction using cycle table:
private void MyInstruction()
{
    // Fetch operand using addressing mode
    byte val = ReadByte(AddrAbsoluteX());
    // Do operation
    A |= val;
    SetZN(A);
    // Add cycles from table
    TotalCycles += (ulong)GetCycleInfo(AddressingMode.AbsoluteX, AccessType.Read).BaseCycles;
}
```

**Testing cycle correctness:**

- `CycleMetadataTests.cs` validates all addressing modes and access types
- Each test loads an instruction, steps the CPU, and asserts both state and cycle count
- Page-cross detection tested with specific address pairs (e.g., $12FF + 0x02 crosses; $1234 + 0x05 does not)
- All 727 tests pass; Klaus Dörmann functional test validates cycle accuracy against real hardware

### Cycle counting rule for write and RMW instructions (legacy)

This is now enforced by the cycle table: Read instructions pay `+1` only when a page boundary is crossed. Write and read-modify-write instructions **always** pay the same cycle count regardless of page crossing — the cost is baked into the `BaseCycles` entry in `CycleTable`.

### Acorn Atom machine (Machines.Atom)

See [docs/atom.md](docs/atom.md) for the full hardware reference. Key classes:

| Class | Role |
|---|---|
| `AtomMachine` | Compositor: wires all chips to the bus and to each other via delegates |
| `Ppi8255` | Intel 8255A PPI at `$B000–$B003`; Port A=keyboard rows, Port B=keyboard cols, Port C=VDG mode + cassette |
| `Mc6847` | VDG; reads `VideoRam.RawBytes` directly; `Control` byte set from `Ppi.PortCLatch` each frame |
| `AtomKeyboardAdapter` | Maps `IPhysicalKeyboard` to the 10×6 Atom key matrix; wired to `Ppi.ReadPortB` |
| `AtomSoundAdapter` | PC4 toggle timestamps → 882-sample 44100 Hz square wave per frame |
| `AtomTapeAdapter` | 300-baud bit streamer; motor on/off is cycle-accurate via `MotorOn(ulong)`/`MotorOff(ulong)` |
| `UefParser` | Parses UEF tape files (gzip-transparent) into `List<bool>` |

Chips are decoupled via delegates: `ppi.ReadPortB = () => kb.ScanColumns(ppi.PortALatch)`. The machine never exposes internal wiring — only `Cpu`, `MainRam`, `VideoRam`, `Ppi`, and `Tape` are public.

### Commodore VIC-20 machine (Machines.Vic20)

See [docs/vic20.md](docs/vic20.md) for the full hardware reference. Key classes:

| Class | Role |
|---|---|
| `Vic20Machine` | Compositor: wires all chips to the bus and to each other via delegates |
| `Via6522` | MOS 6522 VIA — two instances; VIA1 at `$9110` (tape/serial), VIA2 at `$9120` (keyboard) |
| `VicI` | MOS 6560/6561 video chip; generates 256×272 PAL frame; also handles sound |
| `Vic20KeyboardAdapter` | Maps `IPhysicalKeyboard` to the VIC-20 keyboard matrix; wired to `Via2.ReadPortA` |
| `Vic20TapeAdapter` | TAP-format tape streamer; motor driven by VIA1 Port B bit 3 |

**Critical address map note:** BASIC ROM (`$C000–$DFFF`) must NOT be mapped at `$A000–$BFFF`. The `$A000–$BFFF` range is the expansion cartridge area (Block 5), unmapped on an unexpanded VIC-20. Mapping BASIC at `$A000` causes the kernal to see open bus (`$FF`) at `$C002` during cold start, making `JMP ($C002)` jump to `$FFFF` instead of the BASIC entry point, which slowly overflows the stack.

IRQ timing: the VIA1 Timer 1 drives the 50 Hz interrupt. The IRQ pin is level-sensitive on the 6502; only notify the CPU (`Cpu.Irq()`) on the false→true edge. The `InterruptEdgeDetector` component handles edge detection and is called in `AdvanceTiming()` to service the interrupt only on rising edges, preventing stack overflow from immediate re-entry after every RTI.

### Component Lifecycle & Initialization Validation

Both `AtomMachine` and `Vic20Machine` implement `IComponent`, which provides lifecycle validation:

```csharp
public interface IComponent
{
    void ValidateInitialization();
}
```

**Usage pattern:**

```csharp
var machine = new AtomMachine(basicRom, osRom);
machine.ValidateInitialization();  // Throws InvalidOperationException if validation fails
```

Validation is **automatically called** at the end of each machine constructor, so machines fail fast with clear error messages if critical dependencies are not properly initialized:

**AtomMachine validates:**
- CPU is initialized (internal, always present)
- Main RAM initialized (non-zero size)
- Video RAM initialized (non-zero size)
- PPI8255 chip initialized

**Vic20Machine validates:**
- CPU is initialized (internal, always present)
- RAM initialized (non-zero size)
- VIA 1 initialized
- VIA 2 initialized

**Optional components (not validated):**
- Keyboard adapter (optional for headless/test use)
- Tape adapter (optional for headless/test use)
- Audio sink (optional for silent/test use)

If validation fails, `InvalidOperationException` is thrown with a clear message. This catches wiring mistakes and configuration errors at startup, not during emulation.

### Test pattern (Cpu6502.Tests)

All CPU tests inherit `CpuFixture`, which wires a 64 KB `Ram` as the bus. Tests write opcodes into RAM at a known address, call `Load()` to set PC via the reset vector, then `Step(n)` to execute instructions.

```csharp
Load(0x0200, 0xA9, 0x42);  // LDA #$42
Step();
Assert.Equal(0x42, Cpu.A);
Assert.Equal(2UL, Cpu.TotalCycles - before);  // always assert cycle count too
```

Every test asserts **both** the observable state (registers/flags/memory) **and** the cycle count.

### Klaus Dörmann integration test

`IntegrationTests.cs` runs the industry-standard 6502 functional test suite. Download `6502_functional_test.bin` from https://github.com/Klaus2m5/6502_65C02_functional_tests and place it in `tests/Cpu6502.Tests/TestData/`. The test loads the binary at `$0000`, sets PC to `$0400`, and asserts the CPU reaches `$3469` (success). If absent the test passes vacuously.

### 6502 hardware quirks implemented

- **Indirect JMP page-wrap bug**: `JMP ($10FF)` reads hi byte from `$1000`, not `$1100` — implemented in `ReadWordBug()`.
- **JSR pushes PC−1**: the return address on the stack is the last byte of the JSR instruction, not the next instruction. RTS adds 1 on the way back.
- **BRK skips the padding byte**: BRK is effectively a 2-byte instruction; RTI returns to `PC+2` of the BRK.
- **Zero-page indexed addressing wraps**: `$FF + X=1` → `$00`, not `$0100`.
- **BCD mode**: implemented in `DoADC` / `DoSBC`; overflow flag is undefined in BCD on NMOS 6502 and is not set.

### Bus error handling: optional validation via IBusValidator

Address mapping misconfigurations (oversized ROM, undersized RAM, overlap bugs) are difficult to debug — they manifest as crashes or silent memory corruption. The `IBusValidator` interface provides optional device-level validation in DEBUG builds only:

```csharp
public interface IBusValidator : IBus
{
    /// <summary>Validate that an offset is within the device's addressable range. 
    /// Throws InvalidOperationException if validation fails.</summary>
    void ValidateAddress(ushort address);
}
```

**Usage pattern:**

1. Device implements `IBusValidator` (e.g., `Ram`, `Rom`):
```csharp
public sealed class Ram : IBusValidator
{
    private int _size;
    public void ValidateAddress(ushort address)
    {
        if (address >= _size)
            throw new InvalidOperationException(
                $"Ram access at 0x{address:X4} exceeds size 0x{_size:X4}");
    }
}
```

2. `AddressDecoder` calls validators in DEBUG mode:
```csharp
#if DEBUG
if (device is IBusValidator validator)
    validator.ValidateAddress(offset);  // offset is zero-based within device
#endif
```

**Key properties:**

- **Optional**: Not all devices need implement `IBusValidator` (e.g., 6522 VIA, 6847 VDG)
- **DEBUG-only**: Validation compiles away in RELEASE builds → zero performance overhead
- **Centralized**: `AddressDecoder` controls validation policy; devices are clean
- **Zero-based addressing**: Validator receives offset from device base, not CPU bus address

**Example misconfigurations caught:**

- Mapping `Rom(0x4000)` to range `$E000–$FFFF` (8 KB address space) — exception on $E000+0x4000 read
- Mapping `Ram(0x2000)` to range `$0000–$7FFF` (32 KB address space) — exception on $0000+0x2000+ write
- Typos in address ranges causing memory stomping

### Interrupt edge detection: level-sensitive pins, edge-triggered CPU

The 6502's IRQ and NMI inputs are **level-sensitive**: they are sampled at each CPU cycle, and if held HIGH, the CPU will service the interrupt. However, **the CPU only acknowledges one interrupt per signal edge** (transition from LOW to HIGH). Without edge detection, a VIA timer that holds IRQ continuously would cause immediate re-entry after every RTI, corrupting the stack.

Machines handle this via the `InterruptEdgeDetector` component in `Machines.Common`:

```csharp
private readonly InterruptEdgeDetector _irqEdge = new();

// In Step() or RunFrame()
bool irqActive = via1.IrqLine || via2.IrqLine;
if (_irqEdge.Detect(irqActive))
    Cpu.Irq();  // Only called on rising edge (LOW → HIGH)
```

The detector maintains internal state (`_lineWasActive`) and returns true only when the line transitions from false to true. This converts level-sensitive hardware into the edge-triggered semantics the 6502 requires.

---

## Display Features

### Display options configuration

Display rendering behavior is controlled via the `DisplayOptions` class in `Adapters.Raylib`:

```csharp
public sealed class DisplayOptions
{
    public int Scale { get; set; } = 3;
    public bool Smooth { get; set; } = false;
    public float ScanlineIntensity { get; set; } = 0f;
}
```

- **Scale**: Window scale factor (default 3, must be ≥1)
- **Smooth**: Enable bilinear texture filtering for smooth scaling (default false)
- **ScanlineIntensity**: CRT scanline effect intensity 0.0–1.0 (default 0.0 = off)

Pass `DisplayOptions` to `RaylibHost` constructor:

```csharp
using var host = new RaylibHost(
    "Acorn Atom",
    new DisplayOptions { Scale = 3, Smooth = true, ScanlineIntensity = 0.5f },
    logKeypresses: false);
```

### Display: bilinear texture filtering (`--smooth`)

The `--smooth` command-line flag enables bilinear filtering on the video texture:

```bash
atom --basic atom-basic.rom --os atom-os.rom --smooth
vic20 --basic basic.rom --kernal kernal.rom --smooth
```

When enabled, `RaylibHost` calls `SetTextureFilter(_texture, TextureFilter.Bilinear)` during initialization. This replaces the default nearest-neighbour filtering with bilinear interpolation, giving smoother scaling at the cost of slight blur.

**Implementation:**
- `DisplayOptions.Smooth` flag controls whether to apply filtering
- `IRaylibBackend.SetTextureFilter()` method wraps Raylib's native texture filtering API
- Applied once at texture load time; no per-frame overhead

### Display: CRT scanlines (`--scanlines <intensity>`)

The `--scanlines` flag adds CRT scanline effect by darkening alternating horizontal rows:

```bash
atom --basic atom-basic.rom --os atom-os.rom --scanlines 0.5
vic20 --basic basic.rom --kernal kernal.rom --scanlines 0.3
```

Intensity ranges 0.0 (off) to 1.0 (full darkness on scanlines). Recommended values: 0.3–0.5 for subtle effect.

**Implementation:**
- CPU-side darkening in `SubmitFrame()` after color conversion
- Processes every second row (y = 1, 3, 5, ...) before GPU upload
- Formula: `pixel_rgb *= (1.0 - intensity)`, leaving alpha unchanged
- Applied per-frame; negligible performance impact on modern hardware

### Display: Runtime hotkey toggles

Both `--smooth` and `--scanlines` can be toggled at runtime with no need to restart:

**F10 — Toggle bilinear texture filtering**
- Toggles between nearest-neighbor and bilinear filtering
- Overlay shows current mode for 1 second ("Bilinear: ON" or "Bilinear: OFF")
- Useful for comparing crisp vs. smooth rendering quality

**F11 — Cycle scanline intensity**
- Cycles through preset intensities: [0.0 (off), 0.3 (subtle), 0.5 (moderate), 1.0 (full)] and back to 0.0
- Overlay shows current intensity ("Scanlines: 0.3" or "Scanlines: OFF")
- Useful for tuning CRT effect without pausing/restarting emulation

**Implementation:**
- `RaylibHost.HandleDisplayHotkeys()` polls `IsKeyDown()` for F10/F11 each frame
- Changes applied immediately via `RaylibHost.ApplyTextureFilter()` and re-render
- `DisplayOptions` properties are mutable to support runtime updates
- Overlay rendered as semi-transparent rectangle with fade effect (175 alpha)

**Command-line usage (both machines):**

```
--smooth              Enable bilinear texture filtering
--scanlines <0..1>    CRT scanline intensity (0 = off, 0.5 = moderate, default 0)
--scale <n>           Window scale factor (default 3)
```

### Acorn Electron machine (`Machines.Electron`)

`docs/electron.md` has a full hardware reference but no implementation exists yet. The Electron shares the 6502 CPU and the same `IBus` / `AddressDecoder` composition pattern. Key chips to emulate: the Ferranti ULA (video, sound, ROM paging, interrupt controller) and the keyboard matrix.

### Execution Tracing & Debugging

The CPU supports pluggable execution tracing for debugging, profiling, and test instrumentation. All tracing is **zero-cost by default** — the `NullTrace` no-op implementation incurs no overhead.

**Core concept:** `IExecutionTrace` interface provides four event hooks:

```csharp
public interface IExecutionTrace
{
    void OnInstructionFetched(ushort pc, byte opcode);      // Before dispatch
    void OnInstructionExecuted(ushort pc, byte opcode, int cycles, byte aAfter, byte flags);  // After completion
    void OnMemoryAccess(ushort address, byte value, bool isWrite);  // Every read/write
    void OnInterrupt(InterruptType type, ushort handlerAddress);    // IRQ/NMI
}
```

**Usage:**

```csharp
// 1. Default: no tracing (zero overhead)
var cpu = new Cpu(bus);  // Cpu.Trace defaults to NullTrace.Instance

// 2. Enable tracing with RecordingTrace (test helper)
var trace = new RecordingTrace();
cpu.Trace = trace;

cpu.Step();  // All events recorded in trace.Instructions, trace.MemoryAccesses, trace.Interrupts

// 3. Export for analysis
string json = trace.ExportJson();
Console.WriteLine(json);  // Pretty-printed JSON with all events

// 4. Implement custom trace (e.g., for GDB integration, breakpoints)
public sealed class MyDebuggerTrace : IExecutionTrace
{
    public void OnInstructionFetched(ushort pc, byte opcode)
    {
        // Check conditional breakpoints
        if (pc == 0x3000)
            Debugger.Break();
    }
    
    public void OnInstructionExecuted(ushort pc, byte opcode, int cycles, byte aAfter, byte flags)
    {
        // Log execution hotspots
        _executionCount[pc]++;
    }
    
    public void OnMemoryAccess(ushort address, byte value, bool isWrite)
    {
        // Memory watchpoints, page-table visualization
    }
    
    public void OnInterrupt(InterruptType type, ushort handlerAddress)
    {
        // Interrupt vector verification, nesting analysis
    }
}
cpu.Trace = new MyDebuggerTrace();
```

**Test example:**

```csharp
[Fact]
public void CPU_TracesInstructionExecution()
{
    var trace = new RecordingTrace();
    var cpu = new Cpu(ram);
    cpu.Trace = trace;
    
    // Load: LDA #$42
    ram.Write(0x0200, 0xA9);
    ram.Write(0x0201, 0x42);
    cpu.Reset();
    
    cpu.Step();
    
    // Verify instruction event
    Assert.Single(trace.Instructions);
    var instr = trace.Instructions[0];
    Assert.Equal(0x0200, instr.Pc);
    Assert.Equal(0xA9, instr.Opcode);
    Assert.Equal(2, instr.Cycles);
    Assert.Equal(0x42, instr.AAfter);  // A register after execution
    
    // Verify memory accesses were traced
    var reads = trace.MemoryAccesses.Where(m => !m.IsWrite).ToList();
    Assert.True(reads.Count >= 3);  // Opcode fetch, operand fetch, (possibly more)
}
```

**Trace enhancements:**

#### Conditional breakpoints (implemented)

`IExecutionTrace.ShouldBreak()` provides CPU-side breakpoint checking. When a breakpoint condition is met, a `BreakException` is thrown, allowing test harnesses and debuggers to pause execution before an instruction executes.

```csharp
public interface IExecutionTrace
{
    /// <summary>Called before instruction execution to check for conditional breakpoints.
    /// Return true to throw BreakException (pauses before instruction executes).
    /// Allows test harnesses and debuggers to implement conditional breakpoints.</summary>
    bool ShouldBreak(ushort pc, byte opcode, byte currentA) => false;
    // ... other methods ...
}
```

**Example: Break on A=0x42 at PC=0x3000:**

```csharp
var trace = new RecordingTrace();
trace.BreakpointCondition = (pc, opcode, a) => pc == 0x3000 && a == 0x42;
cpu.Trace = trace;

try
{
    cpu.Step();  // Throws BreakException if condition is met
}
catch (BreakException ex)
{
    Console.WriteLine($"Breakpoint at PC=0x{ex.PC:X4}, A=0x{ex.AValue:X2}");
    // Can resume execution with another Step()
    cpu.Step();  // Executes the breakpoint instruction
}
```

**Breakpoint behavior:**

1. Before each instruction, `Cpu.Step()` peeks at the opcode and checks `ShouldBreak()`
2. If the condition returns true, `BreakException` is thrown immediately
3. The instruction has NOT executed yet—PC is at the breakpoint address
4. Caller can resume execution with another `Step()`, or inspect CPU state before continuing
5. `RecordingTrace.BreakpointHits` tracks all breakpoint hits for analysis

**RecordingTrace breakpoint support:**

- `BreakpointCondition` — optional `Func<ushort, byte, byte, bool>` predicate
- `BreakpointHits` — list of breakpoint triggers: `List<(ushort Pc, byte Opcode, byte AValue)>`
- `ShouldBreak(pc, opcode, a)` — evaluates the condition and throws `BreakException` if true

#### Memory dump export (implemented)

`RecordingTrace` exports instruction and memory access history in CSV and binary formats:

```csharp
var trace = new RecordingTrace();
cpu.Trace = trace;
// ... run code ...

// CSV (human-readable)
File.WriteAllText("instructions.csv", trace.ExportInstructionsCSV());
File.WriteAllText("memory.csv", trace.ExportMemoryAccessesCSV());

// Binary (compact, little-endian)
File.WriteAllBytes("instructions.bin", trace.ExportInstructionsBinary());
File.WriteAllBytes("memory.bin", trace.ExportMemoryAccessesBinary());
```

**CSV format:**
- Instructions: `PC,Opcode,Cycles,A,Flags` (hex)
- Memory: `Address,Value,Operation,Cycles` (hex/text)

**Binary format:**
- Compact little-endian encoding: count (int) + records
- Instructions: ushort pc, byte opcode, int cycles, byte a, byte flags
- Memory: ushort address, byte value, bool isWrite, ulong cycles

Enable external analysis workflows:
- Import CSV into spreadsheet tools for filtering, sorting, charting
- Parse binary format in Python/Rust for custom analysis
- Debug performance issues by correlating cycles and memory patterns

#### Cycle provenance tracking (implemented)

`RecordingTrace` tracks cycle contributions per instruction for performance profiling:

```csharp
var trace = new RecordingTrace();
cpu.Trace = trace;
// ... run code ...

// Query cycle provenance
ulong totalCycles = trace.GetTotalCycles();
ulong romCycles = trace.GetCyclesForAddressRange(0x8000, 0xBFFF);  // ROM only
int lda_cycles = trace.GetCyclesForInstruction(0x1234);  // All invocations at 0x1234
var cyclesByOp = trace.GetCyclesByOpcode();  // Cycles per opcode

// Export for analysis
File.WriteAllText("provenance.csv", trace.ExportCycleProvenanceCSV());
File.WriteAllText("provenance-summary.csv", trace.ExportCycleProvenanceSummaryByRange(
    ("ROM", 0x8000, 0xBFFF),
    ("RAM", 0x0000, 0x7FFF),
    ("Stack", 0x0100, 0x01FF)
));
```

Cycle provenance enables:
- Identify code hot-spots by cycle consumption
- Profile ROM vs RAM execution time
- Analyze function-level cycle costs
- Detect infinite loops or stalled code

**Data structure:**

```csharp
public sealed record CycleContribution(
    ushort Pc,            // Instruction address
    byte Opcode,          // Opcode executed
    int CyclesContributed // Cycles consumed by this instruction
);

public List<CycleContribution> CycleProvenance { get; }  // Recorded for every instruction
```

**Helper methods:**

- `GetTotalCycles()` — sum of all cycles
- `GetCyclesForAddressRange(min, max)` — cycles in address range
- `GetCyclesForInstruction(pc)` — total cycles for all invocations at PC
- `GetCyclesByOpcode()` — cycles grouped by opcode type
- `ExportCycleProvenanceCSV()` — per-instruction breakdown with cumulative percentages
- `ExportCycleProvenanceSummaryByRange(params (name, min, max)[])` — area summary

**Future extensions:**

- **GDB integration**: Implement Remote Serial Protocol (RSP) adapter; connect `rr (record and replay)` / `gdb` to step through emulation
- **VS Code debugger**: Language Server Protocol (LSP) adapter for IDE breakpoints and variable inspection
- **Performance visualization**: Generate flame graphs, timeline views, or interactive profiling UI from cycle provenance data

---

## Code Search

Use `semble search` to find code by describing what it does or naming a symbol/identifier, instead of grep:

​```bash
semble search "authentication flow" ./my-project
semble search "save_pretrained" ./my-project
semble search "save model to disk" ./my-project --top-k 10
​```

Use `semble find-related` to discover code similar to a known location (pass `file_path` and `line` from a prior search result):

​```bash
semble find-related src/auth.py 42 ./my-project
​```

`path` defaults to the current directory when omitted; git URLs are accepted.

If `semble` is not on `$PATH`, use `uvx --from "semble[mcp]" semble` in its place.

## Workflow

1. Start with `semble search` to find relevant chunks.
2. Inspect full files only when the returned chunk is not enough context.
3. Optionally use `semble find-related` with a promising result's `file_path` and `line` to discover related implementations.
4. Use grep only when you need exhaustive literal matches or quick confirmation of an exact string.

