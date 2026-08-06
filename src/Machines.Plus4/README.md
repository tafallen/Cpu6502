# Commodore Plus/4 & C16 Emulator (`Machines.Plus4`)

A C# / .NET 8 emulator for the **Commodore Plus/4 and Commodore 16 (C16)** 264-series computers (1984).

## Hardware Specifications
- **CPU**: MOS 7501 / 8501 @ 0.89 MHz / 1.76 MHz (dynamic fast-clock during blanking)
- **RAM**: 16 KB (C16) or 64 KB (Plus/4)
- **Video & Sound**: MOS 7360 / 8360 TED (320×200 121-color video, 2 sound channels, 2 16-bit timers)

## Quick Start

```bash
# Commodore Plus/4
dotnet run --project src/Host.Plus4 -- --model plus4 --kernal roms/plus4/kernal.bin --basic roms/plus4/basic.bin

# Commodore 16
dotnet run --project src/Host.Plus4 -- --model c16 --kernal roms/plus4/kernal.bin --basic roms/plus4/basic.bin
```
