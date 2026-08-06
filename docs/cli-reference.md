# Command-Line Interface Reference

This document comprehensively documents all command-line arguments for both emulators.

## Acorn Atom

### Required arguments

```bash
dotnet run --project src/Host.Atom -- --basic <path> --os <path>
```

| Argument | File | Size | Description |
|---|---|---|---|
| `--basic <path>` | `abasic.rom` | 4 KB | BASIC ROM — provides BASIC interpreter |
| `--os <path>` | `akernel.rom` | 4 KB | OS/Kernel ROM — provides OS kernel and reset handler |

### Optional ROM arguments

| Argument | File | Size | Description | Address | Fallback |
|---|---|---|---|---|---|
| `--float <path>` | `afloat.rom` | 4 KB | Floating-point arithmetic ROM | $D000-$DFFF | Stub with RTS |
| `--dos <path>` | `dosrom.rom` | 4 KB | DOS/extension ROM | $E000-$EFFF | Stub with RTS |
| `--ext <path>` | `axr1.rom` | 4 KB | Utility ROM for socket #A | $A000-$AFFF | Stub (PLA/RTI) |
| `--char <path>` | (MC6847 character set) | 768 bytes | MC6847 character ROM — 64 chars × 12 rows | Internal | Built-in default |

### Optional display arguments

| Argument | Default | Range | Description |
|---|---|---|---|
| `--scale <n>` | 3 | 1–∞ | Window scale factor → resolution 256×192 × scale |
| `--smooth` | off | bool | Enable bilinear texture filtering for smooth scaling |
| `--scanlines <f>` | 0 | 0.0–1.0 | CRT scanline intensity (0 = off, 0.5 = moderate, 1 = full) |

### Optional debug arguments

| Argument | Description |
|---|---|
| `--debug-keys` | Log raw Raylib keypresses to console (useful for key mapping debugging) |
| `--tape <path>` | Load UEF tape image (plain or gzip-compressed) |

### Examples

```bash
# Minimal: just the two required ROMs
dotnet run --project src/Host.Atom -- --basic abasic.rom --os akernel.rom

# With floating-point support
dotnet run --project src/Host.Atom -- --basic abasic.rom --os akernel.rom --float afloat.rom

# With tape game
dotnet run --project src/Host.Atom -- --basic abasic.rom --os akernel.rom --tape game.uef

# Full setup: all ROMs, tape, and display tweaks
dotnet run --project src/Host.Atom -- \
  --basic abasic.rom \
  --os akernel.rom \
  --float afloat.rom \
  --dos dosrom.rom \
  --ext axr1.rom \
  --tape game.uef \
  --smooth \
  --scanlines 0.5 \
  --scale 4

# Scaled up for visibility
dotnet run --project src/Host.Atom -- --basic abasic.rom --os akernel.rom --scale 4

# Smooth scaling with subtle CRT effect
dotnet run --project src/Host.Atom -- --basic abasic.rom --os akernel.rom --smooth --scanlines 0.3
```

### Runtime hotkeys

While the emulator is running:

| Hotkey | Effect |
|---|---|
| `F10` | Toggle bilinear texture filtering (toggles `--smooth`) |
| `F11` | Cycle scanline intensity through [off, 0.3, 0.5, 1.0] |
| `ESC` | Quit emulator |

An overlay appears for 1 second confirming each change.

---

## Acorn BBC Micro (`Host.BbcMicro`)

```bash
dotnet run --project src/Host.BbcMicro -- --model b --os roms/bbcmicro/os12.rom --basic roms/bbcmicro/basic2.rom --tube --scale 3
```

| Argument | Default | Description |
|---|---|---|
| `--model <m>` | `b` | Hardware model variant (`a` = 16 KB RAM, `b` = 32 KB RAM, `b+` / `bplus64` = 64 KB RAM) |
| `--os <path>` | `roms/bbcmicro/os12.rom` | 16 KB OS 1.20 ROM file ($C000–$FFFF) |
| `--basic <path>` | `roms/bbcmicro/basic2.rom` | 16 KB BASIC 2.0 ROM file ($8000–$BFFF / Sideways Bank 15) |
| `--tube` | off | Enable 65C02 Turbo 4 MHz Second Processor (Co-Processor card) |
| `--scale <n>` | 3 | Window scale factor (640×256 × scale) |
| `--smooth` | off | Enable bilinear texture filtering |
| `--scanlines <f>` | 0 | CRT scanline intensity (0.0–1.0) |

---

## Commodore VIC-20

### Required arguments

```bash
dotnet run --project src/Host.Vic20 -- --basic <path> --kernal <path>
```

| Argument | File | Size | Description |
|---|---|---|---|
| `--basic <path>` | `basic.901486-01.bin` | 8 KB | BASIC V2 ROM — provides BASIC interpreter |
| `--kernal <path>` | `kernal.901486-07.bin` | 8 KB | Kernal ROM (PAL) — provides OS kernel and reset handler |

**Note:** Both ROM dumps are widely available from community ROM archives (search for "VIC-20 ROM set").

### Optional ROM arguments

| Argument | File | Size | Description | Fallback |
|---|---|---|---|---|
| `--char <path>` | `chargen.901460-03.bin` | 4 KB | Character generator ROM — provides on-screen glyphs | Blank glyphs |

### Optional media arguments

| Argument | Format | Description |
|---|---|---|
| `--tape <path>` | TAP (v0 or v1 extended) | Load Commodore tape image |

### Optional display arguments

| Argument | Default | Range | Description |
|---|---|---|---|
| `--scale <n>` | 3 | 1–∞ | Window scale factor → resolution 256×272 × scale |
| `--smooth` | off | bool | Enable bilinear texture filtering for smooth scaling |
| `--scanlines <f>` | 0 | 0.0–1.0 | CRT scanline intensity (0 = off, 0.5 = moderate, 1 = full) |

### Optional debug arguments

| Argument | Description |
|---|---|
| `--debug-keys` | Log raw Raylib keypresses to console (useful for key mapping debugging) |

### Examples

```bash
# Minimal: just the two required ROMs (character ROM optional)
dotnet run --project src/Host.Vic20 -- \
  --basic basic.901486-01.bin \
  --kernal kernal.901486-07.bin

# With character ROM for proper text display
dotnet run --project src/Host.Vic20 -- \
  --basic basic.901486-01.bin \
  --kernal kernal.901486-07.bin \
  --char chargen.901460-03.bin

# With tape game
dotnet run --project src/Host.Vic20 -- \
  --basic basic.901486-01.bin \
  --kernal kernal.901486-07.bin \
  --tape game.tap

# Full setup with display tweaks
dotnet run --project src/Host.Vic20 -- \
  --basic basic.901486-01.bin \
  --kernal kernal.901486-07.bin \
  --char chargen.901460-03.bin \
  --tape game.tap \
  --smooth \
  --scanlines 0.5 \
  --scale 4

# Smooth scaling (VIC-20 benefits from smoothing due to lower resolution)
dotnet run --project src/Host.Vic20 -- \
  --basic basic.901486-01.bin \
  --kernal kernal.901486-07.bin \
  --smooth \
  --scanlines 0.3 \
  --scale 5
```

### Runtime hotkeys

While the emulator is running:

| Hotkey | Effect |
|---|---|
| `F10` | Toggle bilinear texture filtering (toggles `--smooth`) |
| `F11` | Cycle scanline intensity through [off, 0.3, 0.5, 1.0] |
| `ESC` | Quit emulator |

An overlay appears for 1 second confirming each change.

---

## Display options details

### Window scaling (`--scale`)

Controls the window size by scaling the native frame resolution.

**Atom:** 256×192 native
- `--scale 1`: 256×192 window
- `--scale 2`: 512×384 window
- `--scale 3`: 768×576 window (default)
- `--scale 4`: 1024×768 window (recommended for high-DPI displays)

**VIC-20:** 256×272 native
- `--scale 1`: 256×272 window
- `--scale 3`: 768×816 window (default)
- `--scale 5`: 1280×1360 window (recommended for proper aspect ratio on large displays)

### Bilinear filtering (`--smooth`)

Enable bilinear texture filtering for smooth (but slightly blurry) scaling instead of crisp pixel-perfect nearest-neighbor filtering.

**Without `--smooth` (default):** Crisp pixel doubling; each pixel becomes a square block. Best for pixel-art aesthetic.

**With `--smooth`:** Bilinear interpolation produces smoother edges but slightly blurred pixels. Better for photographic realism.

**Runtime toggle:** Press `F10` to toggle without restarting.

### CRT scanlines (`--scanlines`)

Add a CRT monitor effect by darkening alternating horizontal rows (scanlines).

**Values:**
- `0` (off, default): No scanlines
- `0.3`: Subtle scanlines (recommended for authentic retro feel)
- `0.5`: Moderate scanlines
- `1.0`: Full darkness on scanlines (extreme vintage look)

**Implementation:** CPU-side pixel darkening; applied every second row before GPU upload. Negligible performance impact.

**Runtime cycling:** Press `F11` to cycle through [off, 0.3, 0.5, 1.0, off, ...] without restarting.

---

## Keyboard layout

### Atom keyboard

The Atom keyboard is a 10×6 matrix mapped to standard US QWERTY:

```
Row\Col   0     1      2     3     4     5
0:       ESC  1!    2@   3#   4$   5%
1:       CTRL  6^   7&   8*   9(   0)
2:       SHF   A    B    C    D    E
3:       ----  F    G    H    I    J
4:       ----  K    L    M    N    O
5:       ENT   P    Q    R    S    T
```

**Special keys:**
- `LShift` / `RShift` → Shift row
- `LCtrl` / `RCtrl` → Control row
- `Escape` → ESC key
- `Return` → Enter key
- `Backspace` → Delete (if available)

### VIC-20 keyboard

The VIC-20 keyboard is an 8×8 matrix. Raylib key mapping follows standard QWERTY conventions.

**Special keys:**
- `LShift` / `RShift` → Shift
- `LCtrl` / `RCtrl` → Control (Commodore key in some contexts)
- `Home` → CLR/Home
- `Escape` → Run/Stop

See the keyboard adapter source for full matrix mapping.

**Debug key logging:**

Use `--debug-keys` to see all key presses logged to console during emulation. Useful for verifying that your keyboard is mapped correctly to the machine's matrix.

```
dotnet run --project src/Host.Atom -- --basic abasic.rom --os akernel.rom --debug-keys
```

Each keypress will appear as:
```
Key: A (Atom row 2, col 1)
```

---

## Performance tips

1. **Larger scale factors** (4+) can reduce performance on older GPUs. Use `--scale 3` (default) on slower hardware.

2. **Bilinear filtering** (`--smooth`) has minimal overhead (single GPU filter mode).

---

## Acorn Electron

### Options

```bash
dotnet run --project src/Host.Electron -- --os roms/electron/os.rom --basic roms/electron/basic.rom
```

| Argument | Default | Description |
|---|---|---|
| `--os <path>` | `roms/electron/os.rom` | OS ROM image (16 KB) |
| `--basic <path>` | `roms/electron/basic.rom` | BBC BASIC II ROM image (16 KB) |
| `--tape <path>` | none | Load cassette image |
| `--scale <n>` | 3 | Window scale factor (640×256 base) |
| `--smooth` | off | Enable bilinear texture filtering |
| `--scanlines <f>` | 0 | CRT scanline intensity (0.0–1.0) |

---

## Error messages

### ROM file not found
```
Unhandled exception: System.IO.FileNotFoundException: Could not find file 'path/to/rom.rom'
```
**Solution:** Verify the file path and that the file exists. Use relative paths from the current working directory.

### ROM file wrong size
```
WARNING: OS ROM is only 2048 bytes — expected 4096 ($1000)
```
**Solution:** Verify you're using the correct ROM file. Atom ROMs are 4 KB; VIC-20 ROMs are 8 KB.

### Invalid tape format
```
Exception: Unknown tape format
```
**Solution:** Ensure you're using the correct tape format:
- Atom: UEF format (`.uef` or `.uef.gz`)
- VIC-20: TAP format (`.tap`, v0 or v1 extended)

---

## Troubleshooting

### Emulator starts but screen is blank
1. Verify all ROM files are loaded (check console output for ROM sizes)
2. Check the reset vector is correct (should be listed in console)
3. Try pressing keys to see if the machine is running

### Keys don't respond
1. Use `--debug-keys` to verify keypresses are being registered
2. Check that Raylib window has focus (click on window if needed)

### Tape loading fails
1. Ensure tape file format matches machine (UEF for Atom, TAP for VIC-20)
2. Check console output for tape loading errors
3. Try running the tape in an original emulator to verify it's valid

### Performance is poor
1. Reduce `--scale` factor
2. Disable `--smooth` and `--scanlines`
3. Close other applications to free CPU/GPU resources

---

## Oric-1 / Oric Atmos (`Host.Oric`)

```bash
dotnet run --project src/Host.Oric -- --os roms/oric/atmos.rom --tape game.tap --scale 3
```

| Argument | Default | Description |
|---|---|---|
| `--os <path>` | `roms/oric/atmos.rom` | 16 KB BASIC / OS ROM file |
| `--tape <path>` | (none) | Load `.tap` cassette image into RAM |
| `--scale <n>` | 3 | Window scale factor |

---

## Commodore PET (`Host.Pet`)

```bash
dotnet run --project src/Host.Pet -- --rom roms/pet/pet2001.rom --prg program.prg --scale 3
```

| Argument | Default | Description |
|---|---|---|
| `--rom <path>` | `roms/pet/pet2001.rom` | 28 KB PET system ROM file |
| `--prg <path>` | (none) | Auto-load `.prg` program file directly into RAM at `$0401` |
| `--scale <n>` | 3 | Window scale factor |

---

## Commodore 64 (`Host.C64`)

```bash
dotnet run --project src/Host.C64 -- --kernal roms/c64/kernal.rom --basic roms/c64/basic.rom --prg game.prg --scale 3
```

| Argument | Default | Description |
|---|---|---|
| `--kernal <path>` | `roms/c64/kernal.rom` | 8 KB KERNAL OS ROM file ($E000–$FFFF) |
| `--basic <path>` | `roms/c64/basic.rom` | 8 KB BASIC 2.0 ROM file ($A000–$BFFF) |
| `--char <path>` | `roms/c64/chargen.rom` | 4 KB Character Generator ROM file ($D000–$DFFF) |
| `--prg <path>` | (none) | Auto-load `.prg` program binary file directly into RAM |
| `--scale <n>` | 3 | Window scale factor |
| `--smooth` | off | Enable bilinear texture filtering for smooth scaling |
| `--scanlines <f>` | 0 | CRT scanline intensity (0.0 = off, 0.5 = moderate, 1.0 = full) |

---

## Commodore Plus/4 & C16 (`Host.Plus4`)

```bash
dotnet run --project src/Host.Plus4 -- --model c16 --kernal roms/plus4/kernal.bin --basic roms/plus4/basic.bin --scale 3
```

| Argument | Default | Description |
|---|---|---|
| `--model <m>` | `plus4` | Hardware model variant (`plus4` = 64 KB RAM, `c16` = 16 KB RAM) |
| `--kernal <path>` | `roms/plus4/kernal.bin` | 16 KB Kernal ROM file |
| `--basic <path>` | `roms/plus4/basic.bin` | 16 KB BASIC 3.5 ROM file |
| `--prg <path>` | (none) | Auto-load `.prg` program binary file directly into RAM |
| `--scale <n>` | 3 | Window scale factor |
| `--smooth` | off | Enable bilinear texture filtering |
| `--scanlines <f>` | 0 | CRT scanline intensity (0.0–1.0) |

---

## Acorn System 1–5 (`Host.AcornSystem`)

```bash
dotnet run --project src/Host.AcornSystem -- --model system3 --rom roms/acornsystem/sys3.rom --scale 3
```

| Argument | Default | Description |
|---|---|---|
| `--model <m>` | `system3` | System model variant (`system1`=512B, `system2`=1KB, `system3`=16KB, `system4`=32KB, `system5`=48KB) |
| `--rom <path>` | `roms/acornsystem/sys3.rom` | System OS / Monitor ROM file |
| `--disc <path>` | (none) | Floppy disc image file (`.dsk` / `.img`) |
| `--scale <n>` | 3 | Window scale factor |
| `--smooth` | off | Enable bilinear texture filtering |
| `--scanlines <f>` | 0 | CRT scanline intensity (0.0–1.0) |

---

## Acorn Communicator (`Host.Communicator`)

```bash
dotnet run --project src/Host.Communicator -- --rom roms/communicator/os.rom --scale 3
```

| Argument | Default | Description |
|---|---|---|
| `--rom <path>` | `roms/communicator/os.rom` | 32 KB Paged OS System ROM file |
| `--scale <n>` | 3 | Window scale factor |
| `--smooth` | off | Enable bilinear texture filtering |
| `--scanlines <f>` | 0 | CRT scanline intensity (0.0–1.0) |


