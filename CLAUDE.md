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
```

Coverage reports are written to `tests/*/coverage/coverage.cobertura.xml` after each `dotnet test` run.

## Repository layout

```
src/
  Cpu6502.Core/       — 6502 CPU, Ram, Rom, AddressDecoder (IBus primitives)
  Machines.Common/    — Shared interfaces: IBus, IVideoSink, IAudioSink, IPhysicalKeyboard, ITapeDevice; components: InterruptEdgeDetector, IComponent, MachineClock, TimingScheduler
  Machines.Atom/      — Acorn Atom hardware emulation
  Adapters.Raylib/    — RaylibHost: window, input, audio (IVideoSink + IPhysicalKeyboard + IAudioSink)
  Host.Atom/          — Console entry point; --basic, --os, --tape, --float, --ext, --scale flags
tests/
  Cpu6502.Tests/      — CPU unit tests + Klaus Dörmann integration test
  Machines.Atom.Tests/ — Atom hardware tests (Ppi8255, Mc6847, keyboard, sound, tape, UEF, machine)
docs/
  walkthrough.md      — IBus / AddressDecoder / CPU tutorial
  atom.md             — Acorn Atom hardware reference: address map, chips, ROM layout, VBL timing
  electron.md         — Acorn Electron hardware reference: ULA, address map, ROM banking, video modes
  vic20.md            — Commodore VIC-20 hardware reference
  vic20-tape.md       — VIC-20 TAP tape format details
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

## Future work

### Display: scaling filter (`--smooth`)

`RaylibHost.SubmitFrame` currently uses `DrawTextureEx` with Raylib's default nearest-neighbour filtering, giving a hard-pixel look. A `--smooth` flag should enable bilinear filtering:

```csharp
Raylib.SetTextureFilter(_texture, TextureFilter.Bilinear);
```

This is a one-line change in `RaylibHost` after the texture is loaded. For higher quality, a GLSL sharp-bilinear or xBR shader could be added later via `LoadShader` / `BeginShaderMode`.

### Display: scanlines (`--scanlines <intensity>`)

Simulate CRT scanline gaps by darkening alternating horizontal rows. Recommended approach is a GPU-side GLSL fragment shader so the effect is free at runtime and trivially togglable:

```glsl
// scanlines.frag (sketch)
uniform float intensity;   // 0.0 = off, 0.5 = half brightness on dark rows
void main() {
    vec4 col = texture(texture0, fragTexCoord);
    if (mod(floor(gl_FragCoord.y), 2.0) == 0.0)
        col.rgb *= (1.0 - intensity);
    finalColor = col;
}
```

Wire up in `RaylibHost`: load the shader once, pass `intensity` as a uniform, wrap `DrawTextureEx` in `BeginShaderMode` / `EndShaderMode`. Scanlines only look good at 2× scale or above.

**Suggested command-line flags** (both machines, added in their respective `Program.cs`):

```
--smooth              Enable bilinear texture filtering
--scanlines <0..1>    Scanline intensity (0 = off, 0.5 = moderate, default 0)
```

**Runtime toggle**: check hotkeys in `PollEvents()` — e.g. `F11` cycles scanline intensity, `F10` toggles smooth. These are pure `RaylibHost` concerns; `IVideoSink` and the machine classes need no changes.

**`DisplayOptions` record** (to avoid threading individual flags through constructors):

```csharp
record DisplayOptions(int Scale = 3, bool Smooth = false, float ScanlineIntensity = 0f);
```

Pass into `RaylibHost` instead of the bare `scale` int.

### Acorn Electron machine (`Machines.Electron`)

`docs/electron.md` has a full hardware reference but no implementation exists yet. The Electron shares the 6502 CPU and the same `IBus` / `AddressDecoder` composition pattern. Key chips to emulate: the Ferranti ULA (video, sound, ROM paging, interrupt controller) and the keyboard matrix.

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

