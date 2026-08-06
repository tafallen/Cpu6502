# Commodore VIC-20 Emulator (`Machines.Vic20`)

A C# / .NET 8 emulator for the **Commodore VIC-20** (1980).

## Hardware Specifications
- **CPU**: MOS 6502 @ 1.108 MHz (PAL)
- **RAM**: 5 KB Base RAM (3.5 KB usable for BASIC, expandable with 3K, 8K, 16K, 24K, 32K RAM cards)
- **Video & Sound**: MOS 6560 / 6561 VIC (176×184 PAL/NTSC video, 4 sound channels)
- **Cartridges**: Support for 4K / 8K / 16K Cartridge ROMs loaded at Block 5 (`$A000`) and Block 3 (`$6000`)

## Quick Start

```bash
# Standard Unexpanded VIC-20
dotnet run --project src/Host.Vic20 -- --basic roms/vic20/basic.bin --kernal roms/vic20/kernal.bin

# Expanded VIC-20 with 16K RAM and Cartridge ROM
dotnet run --project src/Host.Vic20 -- --ram 16k --cart roms/vic20/game.crt --basic roms/vic20/basic.bin --kernal roms/vic20/kernal.bin
```
