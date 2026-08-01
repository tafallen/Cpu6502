# Atari 800XL Microcomputer Architecture & Setup

The **Atari 800XL** (released 1983) is an 8-bit home computer based on the SALLY 6502C CPU running at 1.79 MHz (NTSC) / 1.77 MHz (PAL).

---

## 1. System Address Map

| Address Range | Device / Chip | Description |
|---|---|---|
| `$0000–$9FFF` | Main RAM | Lower 40 KB System RAM |
| `$A000–$BFFF` | RAM / BASIC ROM | 8 KB BASIC ROM (enabled when PIA `PORTB` bit 1 = 0) |
| `$C000–$CFFF` | Main RAM | Upper RAM |
| `$D000–$D0FF` | GTIA | Color palette, player-missile sprites, collision registers |
| `$D200–$D2FF` | POKEY | 4-channel audio sound synthesizer, serial I/O, keyboard matrix |
| `$D300–$D3FF` | PIA 6520 | Joystick ports & PORTB memory banking control |
| `$D400–$D4FF` | ANTIC | Display List DMA engine & NMIs |
| `$D800–$FFFF` | OS ROM | 10 KB System OS Kernel (enabled when PIA `PORTB` bit 0 = 0) |

---

## 2. Command-Line Usage (`Host.Atari800`)

```bash
dotnet run --project src/Host.Atari800 -- --os roms/atari/atarixl.rom --basic roms/atari/basic.rom --xex game.xex --scale 3
```

| Flag | Description |
|---|---|
| `--os <path>` | Path to 16 KB Atari 800XL OS ROM file |
| `--basic <path>` | Path to 8 KB Atari BASIC ROM file |
| `--xex <path>` | Path to `.xex` Atari executable file to auto-load |
| `--atr <path>` | Path to `.atr` floppy disk image |
| `--scale <n>` | Window scale factor (default: 3) |
| `--smooth` | Enable bilinear texture filtering |
| `--scanlines <f>` | CRT scanline intensity (0.0 to 1.0) |
