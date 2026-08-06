# Acorn Atom Emulator (`Machines.Atom`)

A high-performance C# / .NET 8 emulator for the **Acorn Atom** (1980) 8-bit microcomputer.

## Hardware Specifications
- **CPU**: MOS 6502 @ 1.0 MHz
- **RAM**: 12 KB Base RAM (expandable to 40 KB)
- **Video**: Motorola MC6847 VDG (256×192 2-color / 128×192 4-color / 32×24 text)
- **I/O**: INS8255 PPI (Keyboard matrix, cassette relay, speaker)

## Quick Start

```bash
dotnet run --project src/Host.Atom -- --basic roms/atom/abasic.rom --os roms/atom/akernel.rom --scale 3
```

## Features
- Full MC6847 Video Display Generator rendering.
- Integrated UEF tape image player (`.uef`).
- Custom ROM socket loading (`--float`, `--dos`, `--ext`).
