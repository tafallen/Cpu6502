# Acorn Communicator Emulator (`Machines.Communicator`)

A C# / .NET 8 emulator for the **Acorn Communicator** business microcomputer (1985).

## Hardware Specifications
- **CPU**: WDC 65C02 @ 2.0 MHz
- **RAM**: 512 KB RAM (32 KB lower RAM window + paged RAM)
- **ROM**: 512 KB Paged OS / Software ROM (View / ViewSheet)
- **I/O**: VIA 6522, Econet, 80-column display controller

## Quick Start

```bash
dotnet run --project src/Host.Communicator -- --rom roms/communicator/os.rom
```
