# Oric-1 / Oric Atmos Target Documentation (`Host.Oric`)

`Host.Oric` is a complete runnable target emulating the **Oric-1** and **Oric Atmos** 6502 microcomputers (1983/1984).

---

## Memory Map

| Address Range | Device | Description |
|---|---|---|
| `$0000–$BFFF` | RAM | 48 KB User RAM |
| `$0300–$030F` | MOS 6522 VIA | Versatile Interface Adapter (I/O, timers, sound/keyboard strobe) |
| `$A000–$BF3F` | Video RAM | HIRES bitmap display buffer (240×176) |
| `$B400–$BFFF` | Font Table | Character generator patterns in RAM |
| `$BB80–$BFDF` | Video RAM | TEXT mode character display buffer (40×28) |
| `$C000–$FFFF` | OS ROM | 16 KB BASIC / System OS ROM |

---

## Hardware Features

* **Processor**: MOS 6502 @ 1.0 MHz.
* **Display**: Custom ULA chip with 240×200 resolution, supporting TEXT mode ($BB80) and HIRES mode ($A000) with Teletext serial attributes ($00–$1F: Ink, Paper, Blink, Flash).
* **Sound**: General Instrument AY-3-8912 Programmable Sound Generator (3 tone channels + 1 noise generator).
* **Cassette I/O**: `.tap` file loader parsing header sync markers, auto-run flags, and loading binary payloads directly to RAM.

---

## Execution Command

```bash
dotnet run --project src/Host.Oric -- --os roms/oric/atmos.rom --tape game.tap --scale 3
```
