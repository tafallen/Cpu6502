# Cpu6502

[![CI](https://github.com/tafallen/Cpu6502/actions/workflows/ci.yml/badge.svg)](https://github.com/tafallen/Cpu6502/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

A cycle-accurate, zero-allocation MOS 6502 CPU emulator written in C#. Includes support for all 151 legal opcodes, precise status flag behaviors, BCD mode, page-cross timing penalties, and the classic indirect-JMP `$xxFF` page-wrap bug.

Designed for composing real 80s machine emulators — the CPU knows nothing about the machine it is in, it only talks to an `IBus`. Other chips and chipsets are also implemented so you can compose the emulator for the 6502 based machine you want. Currently the *Vic20*, *Acorn Atom* and *Acorn Electron* are supported with more to come.

Oh, and it's __fast__ for a C# emulator. Really, really fast.

> 🚀 **Performance Headline**: **`Cpu6502` is the fastest cycle-accurate 6502 CPU emulator written in C#**, reaching **> 107.9 Million instructions/second** (**9.38 ns** per mixed opcode) and **0 Bytes managed heap allocations**.

---

## Quick Start

```csharp
using Cpu6502.Core;

// 1. Create a 64 KB flat RAM bus and load program at $0200
var ram = new Ram(0x10000);
ram.Load(0x0200, new byte[] { 0xA9, 0x01, 0x69, 0x01, 0x8D, 0x00, 0x03, 0x00 });

// 2. Set RESET vector to $0200
ram.Write(0xFFFC, 0x00);
ram.Write(0xFFFD, 0x02);

// 3. Instantiate CPU, reset, and step
var cpu = new Cpu(ram);
cpu.Reset();

while (ram.Read(0x0300) == 0)
    cpu.Step();

Console.WriteLine($"Result: {ram.Read(0x0300)}, Cycles: {cpu.TotalCycles}"); // → Result: 2
```

---

## Performance Summary

| Component | Benchmark | Latency | Peak Throughput | Allocation |
|---|---|---|---|---|
| **CPU Execution Engine** | `Step_Mix_LoadStoreBranch` | **9.38 ns** / opcode | **> 107.9 Million opcodes/sec** | **0 B** |
| **Address Bus Decoder** | `Read_RAM_1M` | **0.66 ns** / access | **> 1.51 Billion bus ops/sec** | **0 B** |
| **MC6847 VDG Display** | `RenderFrame_100` | **13.86 μs** / 100 frames | **> 7.2 Million frames/sec** | **0 B (Zero GC)** |
| **VIC-I Video Chip** | `RenderFrame_100` | **12.35 μs** / 100 frames | **> 8.0 Million frames/sec** | **0 B (Zero GC)** |
| **SIMD Pixel Converter** | ARGB32 → RGBA32 | **39.18 ns** / frame | **> 25.5 Million frames/sec** | **0 B** |

For detailed competitive analysis against other 6502 emulators, see **[docs/performance-comparison.md](docs/performance-comparison.md)**.

---

## Emulated Machine Targets

`Cpu6502` includes full runnable machine target executables built on top of the core CPU:

* 🕹️ **Acorn Atom** (`Host.Atom`): Full 6502 @ 1 MHz, Intel 8255 PPI, Motorola MC6847 VDG, cassette UEF playback, and sound. See **[docs/atom.md](docs/atom.md)**.
* 🕹️ **Commodore VIC-20** (`Host.Vic20`): Full 6502 @ 1.108 MHz, MOS 6560/6561 VIC-I video/audio, dual MOS 6522 VIAs, and TAP cassette support. See **[docs/vic20.md](docs/vic20.md)**.
* 🕹️ **Acorn Electron** (`Host.Electron`): Full 6502 @ 2 MHz, ULA video modes 0–6, paged ROM switching, and cassette I/O. See **[docs/electron.md](docs/electron.md)**.

---

## Documentation Directory

| Document | Description |
|---|---|
| 📖 **[docs/walkthrough.md](docs/walkthrough.md)** | Full tutorial on building custom 6502 machines and buses |
| ⚡ **[docs/performance-comparison.md](docs/performance-comparison.md)** | Deep technical comparison against other open-source 6502 emulators |
| 📊 **[docs/technical-debt-and-performance-analysis.md](docs/technical-debt-and-performance-analysis.md)** | Architectural design analysis and optimization history |
| 💻 **[docs/cli-reference.md](docs/cli-reference.md)** | Complete CLI command-line reference, flags, and hotkeys |
| 🖥️ **[docs/atom.md](docs/atom.md)** | Acorn Atom address map, hardware registers, and ROM setup |
| 🖥️ **[docs/vic20.md](docs/vic20.md)** | Commodore VIC-20 address map, chip specs, and TAP guide |
| 🖥️ **[docs/electron.md](docs/electron.md)** | Acorn Electron ULA modes, address layout, and banking |

---

## Correctness & Verification

The CPU engine is verified against **[Klaus Dörmann's 6502 Functional Test Suite](https://github.com/Klaus2m5/6502_65C02_functional_tests)**.

```bash
dotnet test
```

To run strict integration tests requiring functional assets:

```bash
CPU6502_REQUIRE_INTEGRATION_ASSETS=1 dotnet test
```

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
