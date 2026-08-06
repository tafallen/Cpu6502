# Commodore 64 Emulator (`Machines.C64`)

A C# / .NET 8 emulator for the **Commodore 64** (1982).

## Hardware Specifications
- **CPU**: MOS 6510 @ 0.985 MHz (PAL) with `$00/$01` on-chip I/O port banking
- **RAM**: 64 KB RAM
- **Video**: MOS 6567 / 6569 VIC-II (320×200 16-color video, 8 hardware sprites, raster interrupts)
- **Audio**: MOS 6581 / 8580 SID (3 synthesizer channels, ADSR, multi-mode filter)
- **I/O**: Dual MOS 6526 CIA 1 and CIA 2 controllers (Keyboard, Joysticks, IEC serial, TOD clock)

## Quick Start

```bash
dotnet run --project src/Host.C64 -- --kernal roms/c64/kernal.rom --basic roms/c64/basic.rom --prg game.prg
```
