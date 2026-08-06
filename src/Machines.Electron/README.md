# Acorn Electron Emulator (`Machines.Electron`)

A C# / .NET 8 emulator for the **Acorn Electron** (1983).

## Hardware Specifications
- **CPU**: MOS 6502 @ 1.0–2.0 MHz (dynamic wait-state clock)
- **RAM**: 32 KB RAM
- **Video & Systems**: Custom Electron ULA (320×256 graphics, cassette audio, interrupt handler)

## Quick Start

```bash
dotnet run --project src/Host.Electron -- --os roms/electron/os.rom --basic roms/electron/basic.rom
```
