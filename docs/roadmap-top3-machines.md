# Project Roadmap & Planning: Top 3 Machine Target Epics

Planning document for the next three MOS 6502 emulator machine targets: **BBC Micro Model B**, **Oric-1 / Oric Atmos**, and **Commodore PET 2001 / 4032 / 8032**.

---

## Epic 1: BBC Micro Model B Emulator (`Machines.BbcMicro` & `Host.BbcMicro`)

### Description
Build a complete Acorn BBC Micro Model B emulator leveraging the existing 6502 CPU core, 6522 VIA controllers, and Raylib host display pipeline.

### User Stories & Implementation Tasks
* **[Story 1.1] Core Memory Bus & Architecture Framework**
  * Create `src/Machines.BbcMicro` and `src/Host.BbcMicro`.
  * Map $0000–$7FFF RAM (32 KB), $8000–$BFFF Sideways ROM slots (16 banks), and $C000–$FFFF OS ROM.
* **[Story 1.2] Display System (Motorola 6845 CRTC & SAA5050 Teletext)**
  * Implement Motorola 6845 CRTC timing and SAA5050 Teletext Mode 7 character renderer.
* **[Story 1.3] Dual VIA 6522 System & User I/O**
  * Wire System VIA (keyboard, IC32 latches, sound) and User VIA (printer, user port) using shared `Via6522`.
* **[Story 1.4] SN76489 Sound Synthesis**
  * Synthesize 4-channel sound (3 tone + 1 noise) into Raylib `IAudioSink` PCM audio stream.
* **[Story 1.5] Acorn DFS Floppy Disc Drive Emulation**
  * Implement 8271 Floppy Disc Controller and `.ssd` / `.dsd` single/double-sided disc image loader.
* **[Story 1.6] Unit & Integration Testing Suite**
  * Create `tests/Machines.BbcMicro.Tests` with OS boot and memory banking tests.

---

## Epic 2: Oric-1 / Oric Atmos Emulator (`Machines.Oric` & `Host.Oric`)

### Description
Build a complete Oric-1 / Oric Atmos emulator taking advantage of maximum component reuse with `Via6522`.

### User Stories & Implementation Tasks
* **[Story 2.1] Core Memory Bus & Host Runner**
  * Create `src/Machines.Oric` and `src/Host.Oric`.
  * Map $0000–$BFDF RAM (48 KB), $C000–$FFFF 16 KB BASIC/OS ROM.
* **[Story 2.2] Oric ULA Video Renderer**
  * Implement 240×200 8-color text and HIRES graphics modes with serial attribute handling.
* **[Story 2.3] MOS 6522 VIA I/O & Keyboard Matrix**
  * Wire MOS 6522 VIA for keyboard scanning matrix and cassette relay.
* **[Story 2.4] AY-3-8912 Programmable Sound Generator**
  * Implement 3-channel PSG sound synthesizer for games and beeper audio.
* **[Story 2.5] Tape Image Loader (`.tap` / `.wav`)**
  * Implement Oric cassette file parser and fast ROM trap loader.
* **[Story 2.6] Unit & Integration Testing Suite**
  * Create `tests/Machines.Oric.Tests` verifying video attributes and ROM execution.

---

## Epic 3: Commodore PET 2001 / 4032 / 8032 Emulator (`Machines.Pet`)

### Description
Build a complete Commodore PET series emulator featuring monochrome video display and IEEE-488 bus peripherals.

### User Stories & Implementation Tasks
* **[Story 3.1] Core Memory Bus & Architecture Framework**
  * Create `src/Machines.Pet` and `src/Host.Pet`.
  * Map $0000–$7FFF RAM, $8000–$87FF Video RAM, $9000–$FFFF Kernel/BASIC/Editor ROMs.
* **[Story 3.2] PET Monochrome Video Display Generator**
  * Implement 40×25 and 80×25 monochrome text/character matrix display modes.
* **[Story 3.3] PIA 6520 & VIA 6522 I/O Controllers**
  * Wire PIA 6520 and VIA 6522 for Chiclet/business keyboard matrices and screen blanking IRQs.
* **[Story 3.4] IEEE-488 Disk Interface & Direct Program Auto-Loader**
  * Implement IEEE-488 bus protocol and `.prg` Commodore program auto-loader.
* **[Story 3.5] Unit & Integration Testing Suite**
  * Create `tests/Machines.Pet.Tests` verifying keyboard matrix, video RAM, and PET ROM boot.

---

## Epic 4: Acorn BBC Master 128 & Tube Coprocessor Interface (`Machines.BbcMaster` & `Host.BbcMaster`)

### Description
Build a complete Acorn BBC Master 128 emulator featuring 128 KB RAM/Shadow RAM, WDC 65C102 processor, WD1770 FDC, MC146818 RTC/CMOS, and the Acorn Tube Coprocessor Interface.

### User Stories & Implementation Tasks
* **[Story 4.1] Core Memory Bus, 128 KB RAM & ACCCON Register ($FE34)** (#22)
  * Create `src/Machines.BbcMaster` and `src/Host.BbcMaster`.
  * Map $0000–$7FFF Main/Shadow RAM, $8000–$BFFF Sideways RAM/ROMs, ACCCON register ($FE34).
* **[Story 4.2] Shadow Video RAM & Dual Display Buffer Switch** (#23)
  * Implement Shadow Video RAM decoupling for full 32 KB user BASIC RAM in Modes 0–6.
* **[Story 4.3] Western Digital 1770 FDC & ADFS Disc Loader (.adf / .adl)** (#24)
  * Implement `Wd1770Fdc.cs` disk controller and double-density ADFS disc image loader.
* **[Story 4.4] MC146818 Real-Time Clock (RTC) & 50-byte CMOS NVRAM** (#25)
  * Implement `Mc146818Rtc.cs` ($FE30/$FE31) storing `*CONFIGURE` settings.
* **[Story 4.5] Tube Interface: Tube ULA Hardware FIFOs & 65C102 Second Processor** (#26)
  * Implement `TubeUla.cs` ($FEE0–$FEEF) 4-FIFO inter-processor communication controller and dual-CPU runner.
* **[Story 4.6] Unit & Integration Testing Suite (Headless MOS 3.20 Boot)** (#27)
  * Create `tests/Machines.BbcMaster.Tests` verifying ACCCON, Shadow RAM, WD1770, RTC, Tube FIFOs, and headless MOS 3.20 boot.
