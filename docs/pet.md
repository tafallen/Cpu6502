# Commodore PET Target Documentation (`Host.Pet`)

`Host.Pet` is a complete runnable target emulating the **Commodore PET 2001 / 4032 / 8032** microcomputers (1977/1980).

---

## Memory Map

| Address Range | Device | Description |
|---|---|---|
| `$0000–$7FFF` | Main RAM | 32 KB User RAM |
| `$8000–$87FF` | Video RAM | 2 KB Monochrome Video RAM (40×25 display) |
| `$9000–$BFFF` | BASIC ROM | BASIC 2.0 / 4.0 ROMs |
| `$E000–$E7FF` | Editor ROM | Screen editor ROM |
| `$E810–$E813` | PIA 6520 | Peripheral Interface Adapter (keyboard matrix column/row scanning) |
| `$E840–$E84F` | VIA 6522 | Versatile Interface Adapter (system timers, CB2 sound, vertical sync) |
| `$F000–$FFFF` | Kernel ROM | System Kernel ROM & CPU vectors |

---

## Hardware Features

* **Processor**: MOS 6502 @ 1.0 MHz.
* **Display**: 40×25 / 80×25 monochrome text display rendering 8×8 PETSCII character matrix patterns with inverse video attribute support.
* **Peripherals**: MOS 6520 PIA, MOS 6522 VIA, and IEEE-488 (GPIB) parallel bus controller.
* **Auto-Loader**: Direct `.prg` binary stream auto-loader loading programs directly into RAM at `$0401`.

---

## Execution Command

```bash
dotnet run --project src/Host.Pet -- --rom roms/pet/pet2001.rom --prg demo.prg --scale 3
```
