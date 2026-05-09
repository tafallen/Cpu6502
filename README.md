# Cpu6502

A cycle-accurate MOS 6502 CPU emulator in C#. All 151 legal opcodes, correct flag behaviour, page-cross timing penalties, BCD mode, and the classic indirect-JMP page-wrap bug.

Designed for composing real 80s machine emulators — the CPU knows nothing about the machine it is in, it only talks to an `IBus`.

## Quick start

```csharp
using Cpu6502.Core;

// 1. Create a 64 KB flat RAM bus
var ram = new Ram(0x10000);

// 2. Load a small program at $0200
ram.Load(0x0200, new byte[]
{
    0xA9, 0x01,   // LDA #$01
    0x69, 0x01,   // ADC #$01
    0x8D, 0x00,   // STA $0300    (lo)
    0x03,         //              (hi)
    0x00          // BRK
});

// 3. Point the RESET vector at the program
ram.Write(0xFFFC, 0x00);
ram.Write(0xFFFD, 0x02);   // → $0200

// 4. Create the CPU, reset, and run
var cpu = new Cpu(ram);
cpu.Reset();

while (true)
{
    cpu.Step();
    if (ram.Read(0x0300) != 0) break;   // wait for result to appear
}

Console.WriteLine($"Result: {ram.Read(0x0300)}");   // → 2
Console.WriteLine($"Cycles: {cpu.TotalCycles}");
```

## Key types

| Type | Purpose |
|---|---|
| `IBus` | Interface the CPU talks to — implement this for any hardware component |
| `Ram` | Flat read/write memory with a `Load(address, bytes[])` helper |
| `Rom` | Read-only memory; silently ignores writes |
| `AddressDecoder` | Routes CPU traffic to components by address range — this is how you build a machine |
| `Cpu` | The 6502 itself: `Reset()`, `Step()`, `Irq()`, `Nmi()` |

## Building a machine

```csharp
var bus = new AddressDecoder();
bus.Map(0x0000, 0x7FFF, new Ram(0x8000));
bus.Map(0x8000, 0xBFFF, new Rom(File.ReadAllBytes("basic.rom")));
bus.Map(0xC000, 0xFFFF, new Rom(File.ReadAllBytes("os.rom")));
bus.Map(0xFE00, 0xFEFF, new MyUla());   // IBus implementation for MMIO

var cpu = new Cpu(bus);
cpu.Reset();
```

See [docs/walkthrough.md](docs/walkthrough.md) for a full tutorial.

## Acorn Atom emulator

A complete Acorn Atom emulator is included in `src/Machines.Atom` and `src/Host.Atom`. It emulates the full hardware stack:

| Component | Class | Hardware |
|---|---|---|
| CPU | `Cpu` | MOS 6502 @ 1 MHz |
| PPI | `Ppi8255` | Intel 8255A — keyboard, cassette I/O, VDG mode pins |
| VDG | `Mc6847` | Motorola MC6847 — 10 display modes, 256×192 output |
| Keyboard | `AtomKeyboardAdapter` | 10×6 key matrix, row-select via Port A |
| Sound | `AtomSoundAdapter` | PC4 bit-banged square wave → 44100 Hz PCM |
| Tape | `AtomTapeAdapter` + `UefParser` | UEF tape images (plain or gzip), 300 baud KCS |

To run:

```
dotnet run --project src/Host.Atom -- --basic atom-basic.rom --os atom-os.rom
dotnet run --project src/Host.Atom -- --basic atom-basic.rom --os atom-os.rom --tape game.uef
dotnet run --project src/Host.Atom -- --basic atom-basic.rom --os atom-os.rom --smooth --scanlines 0.5
```

See [docs/atom.md](docs/atom.md) for the full address map, chip documentation, and ROM details. See [docs/cli-reference.md](docs/cli-reference.md) for comprehensive command-line options, display hotkeys (F10/F11), and troubleshooting.

## Commodore VIC-20 emulator

A complete VIC-20 (unexpanded PAL) emulator is included in `src/Machines.Vic20` and `src/Host.Vic20`.

| Component | Class | Hardware |
|---|---|---|
| CPU | `Cpu` | MOS 6502 @ 1.108 MHz |
| Video | `VicI` | MOS 6560/6561 — text mode, 16 colours, 256×272 PAL output |
| I/O 1 | `Via6522` | MOS 6522 VIA 1 — serial bus, tape, joystick |
| I/O 2 | `Via6522` | MOS 6522 VIA 2 — keyboard matrix, joystick |
| Keyboard | `Vic20KeyboardAdapter` | 8×8 key matrix, column-select via VIA 2 Port B |
| Tape | `Vic20TapeAdapter` + `TapParser` | Commodore TAP images (v0 and v1 extended) |

To run you need three ROM images from the VIC-20 (these are freely available from community ROM archives):

| File | Size | Description |
|---|---|---|
| `basic.901486-01.bin` | 8 KB | BASIC ROM |
| `kernal.901486-07.bin` | 8 KB | Kernal ROM (PAL) |
| `chargen.901460-03.bin` | 4 KB | Character ROM |

```
dotnet run --project src/Host.Vic20 -- --basic basic.901486-01.bin --kernal kernal.901486-07.bin --char chargen.901460-03.bin
dotnet run --project src/Host.Vic20 -- --basic basic.901486-01.bin --kernal kernal.901486-07.bin --char chargen.901460-03.bin --tape game.tap
dotnet run --project src/Host.Vic20 -- --basic basic.901486-01.bin --kernal kernal.901486-07.bin --char chargen.901460-03.bin --smooth --scanlines 0.3
```

See [docs/vic20.md](docs/vic20.md) for the full address map, chip documentation, and ROM details. See [docs/cli-reference.md](docs/cli-reference.md) for comprehensive command-line options, display hotkeys (F10/F11), and troubleshooting.

## Validating correctness

Drop `6502_functional_test.bin` from [Klaus Dörmann's test suite](https://github.com/Klaus2m5/6502_65C02_functional_tests) into `tests/Cpu6502.Tests/TestData/` and run `dotnet test`. The integration test will confirm the CPU runs the suite to completion at PC=`$3469`.

By default, this integration test is permissive when the asset is missing (to keep local setup friction low). To require integration assets, set:

```
CPU6502_REQUIRE_INTEGRATION_ASSETS=1
```

When strict mode is enabled, missing assets fail the test with a clear error message.

## Execution tracing & debugging

The CPU supports pluggable execution tracing for debugging, profiling, and analysis. See [CLAUDE.md](CLAUDE.md#execution-tracing--debugging) for:

- **Conditional breakpoints** — Set `BreakpointCondition` on `RecordingTrace` to break on PC, opcode, or accumulator values
- **Memory dump export** — Export instruction and memory access history to CSV or binary formats for spreadsheet/custom analysis
- **Pluggable trace hooks** — Implement `IExecutionTrace` interface for custom debugging workflows

**Quick example:**

```csharp
var trace = new RecordingTrace { BreakpointCondition = (pc, op, a) => pc == 0x3000 };
cpu.Trace = trace;

// ... run code ...

// Export results
File.WriteAllText("trace.csv", trace.ExportInstructionsCSV());
File.WriteAllBytes("memory.bin", trace.ExportMemoryAccessesBinary());
```

## Contributor checklist

When changing emulator behavior, update docs in the same PR:

1. Address map/wiring changes: update `docs/atom.md` or `docs/vic20.md` and any XML comments on constructors/machine maps.
2. Timing/interrupt scheduling changes: update the machine docs where timing behavior is described.
3. Host CLI changes: update host usage text and command-line options tables in docs.
4. Integration tests with external assets: document required files and strict-mode behavior.
