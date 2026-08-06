# Acorn BBC Master 128 Emulator (`Machines.BbcMaster`)

A C# / .NET 8 emulator for the **Acorn BBC Master 128** (1986).

## Hardware Specifications
- **CPU**: WDC 65C02 @ 2.0 MHz
- **RAM**: 128 KB (64 KB Main + 64 KB Shadow / Sideways RAM)
- **Video**: MC6845 CRTC & Video ULA (Mode 0–7 with Shadow Video RAM)
- **Co-Processor**: Tube ULA FIFO controller for external co-processors

## Quick Start

```bash
dotnet run --project src/Host.BbcMaster -- --rom roms/bbcmaster/mos320.rom
```
