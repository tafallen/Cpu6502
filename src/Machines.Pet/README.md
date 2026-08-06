# Commodore PET Emulator (`Machines.Pet`)

A C# / .NET 8 emulator for the **Commodore PET 2001 / 4032 / 8032** series (1977–1980).

## Hardware Specifications
- **CPU**: MOS 6502 @ 1.0 MHz
- **RAM**: 8 KB / 16 KB / 32 KB RAM
- **Video**: Discrete TTL / 6545 CRTC video generator (40×25 / 80×25 monochrome text display)
- **I/O**: Dual PIA 6520 and VIA 6522 controllers

## Quick Start

```bash
dotnet run --project src/Host.Pet -- --rom roms/pet/pet2001.rom --prg program.prg
```
