# Acorn System 1–5 Emulator (`Machines.AcornSystem`)

A C# / .NET 8 emulator for the **Acorn System 1, System 2, System 3, System 4, and System 5** Eurocard rack computers (1979–1982).

## Models Supported
- **System 1**: 512B RAM, 512B CUTS OS ROM, keypad & 7-seg LED display.
- **System 2**: 1 KB RAM, 2 KB OS ROM.
- **System 3**: 16 KB RAM, MC6847 VDG Card (256×192 video), FDC controller.
- **System 4**: 32 KB RAM, dual 5.25" floppy drives.
- **System 5**: 48 KB RAM, 8" floppy drives.

## Quick Start

```bash
dotnet run --project src/Host.AcornSystem -- --model system3 --rom roms/acornsystem/sys3.rom
```
