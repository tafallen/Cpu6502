# Oric-1 / Oric Atmos Emulator (`Machines.Oric`)

A C# / .NET 8 emulator for the **Oric-1 and Oric Atmos** microcomputers (1983–1984).

## Hardware Specifications
- **CPU**: MOS 6502A @ 1.0 MHz
- **RAM**: 16 KB / 64 KB RAM
- **Video**: ULA custom video generator (240×200 8-color graphics & text display)
- **Audio**: AY-3-8912 3-channel sound generator driven by VIA 6522

## Quick Start

```bash
dotnet run --project src/Host.Oric -- --os roms/oric/atmos.rom --tape game.tap
```
