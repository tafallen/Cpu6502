# Acorn BBC Master 128 Technical Documentation

This document describes the memory map, Access Control Register (ACCCON), WD1770 Floppy Disk Controller, Motorola MC146818 RTC/CMOS RAM, and Acorn Tube Coprocessor Interface implemented in `Machines.BbcMaster` and `Host.BbcMaster`.

---

## 1. Memory Map

| Address Range | Size | Component / Purpose |
|---|---|---|
| `$0000–$7FFF` | 32 KB | Main RAM (Default user BASIC memory) |
| `$0000–$7FFF` | 32 KB | Shadow Video RAM (Selectable via ACCCON bit 1) |
| `$8000–$BFFF` | 16 KB | Sideways RAM/ROM Banks (16 slots × 16 KB) |
| `$C000–$FFFF` | 16 KB | MOS 3.20 OS ROM |
| `$FE30–$FE31` | 2 B | MC146818 Real-Time Clock & CMOS RAM |
| `$FE34`       | 1 B | ACCCON Access Control Register |
| `$FE40–$FE4F` | 16 B | System VIA (MOS 6522) |
| `$FE60–$FE6F` | 16 B | User VIA (MOS 6522) |
| `$FE80–$FE83` | 4 B | Western Digital 1770 FDC |
| `$FEE0–$FEEF` | 16 B | Acorn Tube ULA Coprocessor FIFOs |

---

## 2. Access Control Register (ACCCON - $FE34)

* **Bit 0 (D)**: Main RAM select (0 = Main RAM, 1 = Shadow RAM).
* **Bit 1 (E)**: Display Video RAM select (0 = Main RAM, 1 = Shadow RAM).
* **Bit 2 (X)**: Execute from Shadow RAM.
* **Bit 3 (Y)**: Private RAM mapping at `$8000–$9FFF` (HAZEL bank).
* **Bit 7 (T)**: 2 MHz Turbo Mode toggle.

---

## 3. Acorn Tube Coprocessor Interface ($FEE0–$FEEF)

The Tube ULA connects the Host I/O processor (BBC Master 128) to an external Second Processor (65C102 @ 3/4 MHz, Z80 @ 6 MHz, 80186 @ 8 MHz, or ARM1) via 4 hardware FIFOs:
* **R1 ($FEE0/$FEE1)**: Asynchronous control & OSBYTE/OSWORD parameter passing.
* **R2 ($FEE2/$FEE3)**: Command line string streaming (`*` OSCLI commands).
* **R3 ($FEE4/$FEE5)**: Fast VDU screen graphics & text stream rendering.
* **R4 ($FEE6/$FEE7)**: High-speed block DMA data streaming.

---

## 4. Running Host.BbcMaster CLI

```bash
dotnet run --project src/Host.BbcMaster -- --os roms/bbcmaster/mos320.rom --adf games/welcome.adf --scale 3
```
