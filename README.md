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

* 🕹️ **Commodore 64** (`Host.C64`): Full MOS 6510 @ 1.023 MHz, `$00/$01` banking, MOS 6567/6569 VIC-II video, dual MOS 6526 CIAs, MOS 6581 SID sound, `.prg` & `.d64` loaders. See **[docs/c64.md](docs/c64.md)**.
* 🕹️ **Acorn BBC Master 128** (`Host.BbcMaster`): WDC 65C102 @ 2 MHz, 128 KB RAM & Shadow Video RAM, ACCCON register, WD1770 FDC, MC146818 RTC/CMOS, and Tube Coprocessor. See **[docs/bbcmaster.md](docs/bbcmaster.md)**.
* 🕹️ **Commodore PET 2001 / 4032 / 8032** (`Host.Pet`): Full 6502 @ 1 MHz, 32 KB RAM, monochrome character video, PIA 6520, VIA 6522, and IEEE-488 bus auto-loader. See **[docs/pet.md](docs/pet.md)**.
* 🕹️ **Oric-1 / Oric Atmos** (`Host.Oric`): Full 6502 @ 1 MHz, ULA 240×200 video with serial attributes, MOS 6522 VIA, AY-3-8912 PSG sound, and TAP cassette loader. See **[docs/oric.md](docs/oric.md)**.
* 🕹️ **Acorn BBC Micro Model B** (`Host.BbcMicro`): Full 6502 @ 2 MHz, Motorola 6845 CRTC, SAA5050 Teletext, dual MOS 6522 VIAs, SN76489 sound, 16-bank Sideways ROMs, and 8271 FDC disk images.
* 🕹️ **Acorn Atom** (`Host.Atom`): Full 6502 @ 1 MHz, Intel 8255 PPI, Motorola MC6847 VDG, cassette UEF playback, and sound. See **[docs/atom.md](docs/atom.md)**.
* 🕹️ **Commodore VIC-20** (`Host.Vic20`): Full 6502 @ 1.108 MHz, MOS 6560/6561 VIC-I video/audio, dual MOS 6522 VIAs, and TAP cassette support. See **[docs/vic20.md](docs/vic20.md)**.
* 🕹️ **Acorn Electron** (`Host.Electron`): Full 6502 @ 2 MHz, ULA video modes 0–6, paged ROM switching, and cassette I/O. See **[docs/electron.md](docs/electron.md)**.

---

## Documentation Directory

| Document | Description |
|---|---|
| 📖 **[docs/walkthrough.md](docs/walkthrough.md)** | Full tutorial on building custom 6502 machines and buses |
| ⚡ **[docs/performance-comparison.md](docs/performance-comparison.md)** | Deep technical comparison against other open-source 6502 emulators |
| 🚀 **[docs/performance-optimizations-epics-1-2-3.md](docs/performance-optimizations-epics-1-2-3.md)** | Empirical Before vs. After performance figures for Epics 1, 2 & 3 |
| 🚀 **[docs/performance-optimizations-epic-4.md](docs/performance-optimizations-epic-4.md)** | Empirical Before vs. After performance figures for Epic 4 (BBC Master & Tube) |
| 🚀 **[docs/performance-optimizations-epic-5.md](docs/performance-optimizations-epic-5.md)** | Empirical Before vs. After performance figures for Epic 5 (Commodore 64) |
| 🚀 **[docs/performance-optimizations-systemwide.md](docs/performance-optimizations-systemwide.md)** | Empirical Before vs. After performance figures for system-wide core optimizations |
| 📊 **[docs/technical-debt-and-performance-analysis.md](docs/technical-debt-and-performance-analysis.md)** | Architectural design analysis and optimization history |
| 💻 **[docs/cli-reference.md](docs/cli-reference.md)** | Complete CLI command-line reference, flags, and hotkeys |
| 🖥️ **[docs/atom.md](docs/atom.md)** | Acorn Atom address map, hardware registers, and ROM setup |
| 🖥️ **[docs/vic20.md](docs/vic20.md)** | Commodore VIC-20 address map, chip specs, and TAP guide |
| 🖥️ **[docs/electron.md](docs/electron.md)** | Acorn Electron ULA modes, address layout, and banking |
| 🖥️ **[docs/oric.md](docs/oric.md)** | Oric-1 / Oric Atmos address map, ULA video, and TAP guide |
| 🖥️ **[docs/pet.md](docs/pet.md)** | Commodore PET address map, PIA/VIA registers, and IEEE-488 bus |
| 🖥️ **[docs/bbcmaster.md](docs/bbcmaster.md)** | Acorn BBC Master 128 address map, ACCCON register, WD1770, and Tube ULA |
| 🖥️ **[docs/c64.md](docs/c64.md)** | Commodore 64 address map, MOS 6510 $00/$01 banking control, and software setup |
| 🏛️ **[docs/bbc-master-architecture-and-gap-analysis.md](docs/bbc-master-architecture-and-gap-analysis.md)** | BBC Master 128 architecture, gap analysis, and 65C102/Shadow RAM roadmap |

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
