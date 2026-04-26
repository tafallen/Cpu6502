# VIC-20 Emulator

## Quick start

```
dotnet run --project src/Host.Vic20 -- \
  --basic basic.901486-01.bin \
  --kernal kernal.901486-07.bin \
  --char chargen.901460-03.bin
```

## ROM images

The VIC-20 requires three ROM dumps. These are widely available from community ROM archives (search for "VIC-20 ROM set"):

| Flag | File | Size | Description |
|------|------|------|-------------|
| `--basic` | `basic.901486-01.bin` | 8 KB | BASIC V2 ROM |
| `--kernal` | `kernal.901486-07.bin` | 8 KB | Kernal ROM (PAL) |
| `--char` | `chargen.901460-03.bin` | 4 KB | Character generator ROM |

The character ROM is optional — if omitted the screen will show blank glyphs.

## Command-line options

| Flag | Default | Description |
|------|---------|-------------|
| `--basic <path>` | required | 8 KB BASIC ROM |
| `--kernal <path>` | required | 8 KB Kernal ROM |
| `--char <path>` | — | 4 KB character ROM |
| `--tape <path>` | — | Commodore TAP tape image |
| `--scale <n>` | 3 | Window scale factor |

## Address map

| Range | Device | Notes |
|-------|--------|-------|
| `$0000–$00FF` | RAM | Zero page |
| `$0100–$01FF` | RAM | Stack |
| `$0200–$03FF` | RAM | System area |
| `$0400–$0FFF` | Open bus | Returns $FF |
| `$1000–$1FFF` | RAM | 4 KB main RAM (unexpanded) |
| `$2000–$7FFF` | Open bus | Expansion RAM area |
| `$8000–$83FF` | Colour RAM | 4-bit per cell, CPU read/write |
| `$9000–$900F` | VIC-I | Video and audio registers |
| `$9110–$911F` | VIA 1 | Serial bus, tape, joystick |
| `$9120–$912F` | VIA 2 | Keyboard matrix, joystick |
| `$A000–$BFFF` | BASIC ROM | 8 KB, read-only |
| `$C000–$DFFF` | Open bus | |
| `$E000–$FFFF` | Kernal ROM | 8 KB; reset vector at `$FFFC/$FFFD` |

The character ROM (4 KB) is **not on the CPU bus** — it is accessed exclusively by the VIC-I chip at VIC address space `$0000–$0FFF`.

## VIC-I ($9000–$900F)

| Register | Address | Description |
|----------|---------|-------------|
| 0 | `$9000` | Horizontal origin (×2 pixels, bits 6–0) |
| 1 | `$9001` | Vertical origin (scan lines) |
| 2 | `$9002` | Columns (bits 6–0) + screen base bit 9 (bit 7) |
| 3 | `$9003` | Rows (bits 6–1) + char height (bit 0) |
| 4 | `$9004` | Raster counter (read-only) |
| 5 | `$9005` | Screen base bits 13–10 (bits 7–4) + char base bits 13–10 (bits 3–0) |
| 10 | `$900A` | Bass oscillator frequency (bits 7–1) + on/off (bit 0) |
| 11 | `$900B` | Alto oscillator |
| 12 | `$900C` | Soprano oscillator |
| 13 | `$900D` | Noise oscillator |
| 14 | `$900E` | Aux colour (bits 7–4) + master volume (bits 3–0) |
| 15 | `$900F` | Background colour (bits 7–4) + border colour (bits 3–1) + reverse (bit 0) |

Default register state matches the unexpanded PAL VIC-20 Kernal startup: 22 columns, 23 rows, screen base `$1E00`, char base `$8000` (VIC addr `$0000` = char ROM).

## VIC-I address space

The VIC-I has a 14-bit (16 KB) address space separate from the CPU bus:

| VIC address | CPU address | Contents |
|-------------|-------------|----------|
| `$0000–$0FFF` | — | Character ROM (VIC-only) |
| `$1000–$1FFF` | `$1000–$1FFF` | Main RAM (screen data lives here) |
| `$2000–$2FFF` | `$0000–$0FFF` | Zero page / stack mirror |
| `$3000–$3FFF` | — | Character ROM mirror |

## VIA 1 ($9110–$911F) — tape and serial

| Bit | Port | Signal |
|-----|------|--------|
| PB3 | Port B out | Tape motor relay (1 = on) |
| CB1 | Input | Tape data in (edge-triggered) |

## VIA 2 ($9120–$912F) — keyboard

| Port | Direction | Signal |
|------|-----------|--------|
| Port B | Output | Column select (active low — drive column bit to 0 to scan it) |
| Port A | Input | Row data (active low — 0 = key pressed in that row) |

The keyboard matrix is 8 columns × 8 rows. See `Vic20KeyboardAdapter.cs` for the full layout.

## Audio

The VIC-I produces four sound channels mixed to mono 16-bit PCM at 44,100 Hz:

| Channel | Register | Type |
|---------|----------|------|
| Bass | `$900A` | Square wave |
| Alto | `$900B` | Square wave |
| Soprano | `$900C` | Square wave |
| Noise | `$900D` | 15-bit Galois LFSR |

Tone frequency: `1,108,405 / 128 / (256 − (reg & 0xFE))` Hz

## Tape format

TAP files use Commodore's pulse-width encoding. See [vic20-tape.md](vic20-tape.md) for the full format and adapter design.

## Machine class

```csharp
var machine = new Vic20Machine(
    basicRom,           // byte[] — 8 KB
    kernalRom,          // byte[] — 8 KB
    charRom:  charRom,  // byte[]? — 4 KB, optional
    keyboard: host,     // IPhysicalKeyboard?
    audio:    host,     // IAudioSink?
    tape:     tape);    // Vic20TapeAdapter?

machine.Reset();

// Per-frame loop:
machine.RunFrame();        // ~22,168 CPU cycles at PAL 50 Hz
machine.RenderFrame(host); // 256×272 ARGB32 → IVideoSink
```
