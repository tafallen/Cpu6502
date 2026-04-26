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
```

See [docs/atom.md](docs/atom.md) for the full address map, chip documentation, and all command-line options.

## Validating correctness

Drop `6502_functional_test.bin` from [Klaus Dörmann's test suite](https://github.com/Klaus2m5/6502_65C02_functional_tests) into `tests/Cpu6502.Tests/TestData/` and run `dotnet test`. The integration test will confirm the CPU runs the suite to completion at PC=`$3469`.
