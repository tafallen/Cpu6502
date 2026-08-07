# Atari Lynx Emulator (`Machines.Lynx`)

A C# / .NET 8 emulator for the **Atari Lynx** handheld color gaming console (1989).

## Hardware Specifications
- **CPU**: WDC 65SC02 @ up to 4.0 MHz
- **RAM**: 64 KB Unified DRAM (`$0000–$FFF7`)
- **Graphics & Sound (MIKEY)**:
  - 160×102 LCD display buffer (4-bit packed pixels)
  - 16-color palette lookup table mapped to 12-bit RGB444 color space (4,096 total colors)
  - 4 8-bit audio channels
  - 8 programmable 16-bit countdown timers
- **Hardware Coprocessor (SUZY)**:
  - 16-bit hardware multiplication and division coprocessor (`$FC52–$FC70`)
  - Hardware sprite blitter engine

## Quick Start

```bash
dotnet run --project src/Host.Lynx -- --cart roms/lynx/game.lnx --scale 4
```
