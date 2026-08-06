# Atari 800XL / 800 Emulator (`Machines.Atari800`)

A C# / .NET 8 emulator for the **Atari 800XL and Atari 800** 8-bit computers (1979–1983).

## Hardware Specifications
- **CPU**: MOS 6502C (SALLY) @ 1.77 MHz (PAL) / 1.79 MHz (NTSC)
- **RAM**: 64 KB RAM
- **Custom Chipset**:
  - **ANTIC**: Video Display Processor (Display lists, fine scrolling, DMA engine)
  - **GTIA**: Color & Graphics Interface (Playfield colors, player/missile graphics, collision detection)
  - **POKEY**: 4-channel audio synthesizer, keyboard scanner, serial I/O, 16-bit LFSR random number generator
  - **PIA 6520**: Joystick ports and RAM/ROM banking bank selector

## Quick Start

```bash
dotnet run --project src/Host.Atari800 -- --os roms/atari800/atarixl.rom --basic roms/atari800/basic.rom
```
