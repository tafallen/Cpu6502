# Acorn BBC Micro Emulator (`Machines.BbcMicro`)

A high-performance C# / .NET 8 emulator for the **Acorn BBC Micro** (Model A, Model B, Model B+ 64K).

## Hardware Specifications
- **CPU**: MOS 6502 / 65C02 @ 2.0 MHz
- **RAM**: 16 KB (Model A), 32 KB (Model B), 64 KB (Model B+ 64K)
- **Video**: MC6845 CRTC & SAA5050 Teletext Generator (640×256 Mode 0–6 & Mode 7 Teletext)
- **Audio**: SN76489 4-channel sound chip
- **Co-Processor**: Optional 65C02 4 MHz Turbo Second Processor over Tube ULA FIFO

## Quick Start

```bash
# Model B
dotnet run --project src/Host.BbcMicro -- --model b --os roms/bbcmicro/os12.rom --basic roms/bbcmicro/basic2.rom

# Model B+ 64K with 65C02 Turbo Tube Co-Processor
dotnet run --project src/Host.BbcMicro -- --model b+ --tube --os roms/bbcmicro/os12.rom --basic roms/bbcmicro/basic2.rom
```
