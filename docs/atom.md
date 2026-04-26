# Acorn Atom emulator

## Running it

```
dotnet run --project src/Host.Atom -- --basic atom-basic.rom --os atom-os.rom
dotnet run --project src/Host.Atom -- --basic atom-basic.rom --os atom-os.rom --tape game.uef
dotnet run --project src/Host.Atom -- --basic atom-basic.rom --os atom-os.rom --tape game.uef.gz --scale 4
```

All `--` flags:

| Flag | Required | Description |
|---|---|---|
| `--basic <path>` | Yes | 8 KB BASIC ROM image |
| `--os <path>` | Yes | 4 KB OS ROM image |
| `--tape <path>` | No | UEF tape image (plain or gzip-compressed) |
| `--float <path>` | No | 4 KB floating-point ROM |
| `--ext <path>` | No | 4 KB extension ROM (socket #A) |
| `--scale <n>` | No | Window scale factor (default: 3 → 768×576) |

---

## Address map

| Range | Device | Size |
|---|---|---|
| `$0000–$7FFF` | Main RAM | 32 KB |
| `$8000–$9FFF` | Video RAM (dual-ported: CPU + VDG) | 8 KB |
| `$A000–$AFFF` | Extension ROM socket #A (optional) | 4 KB |
| `$B000–$B003` | 8255 PPI (keyboard, cassette, VDG mode) | 4 bytes |
| `$C000–$CFFF` | Floating-point ROM (optional) | 4 KB |
| `$D000–$EFFF` | BASIC ROM | 8 KB |
| `$F000–$FFFF` | OS ROM (reset vector at `$FFFC`/`$FFFD`) | 4 KB |

Unmapped regions return `$FF` (open bus). The real Atom had 2 KB of on-board RAM; mapping the full 32 KB just means the top of that window is always available.

---

## Chips

### Intel 8255A PPI — `Ppi8255.cs`

The Programmable Peripheral Interface sits at `$B000–$B003` and is the hub for keyboard input, cassette I/O, and VDG mode control.

| Register offset | Direction | Connected to |
|---|---|---|
| 0 — Port A | Output | Keyboard row select (value 0–9) |
| 1 — Port B | Input | Keyboard column readback (active low) |
| 2 — Port C | Mixed | Lower nibble: output → VDG mode + cassette out; Upper nibble: input ← cassette in + motor sense |
| 3 — Control | Write-only | Mode/direction configuration word |

The OS configures the PPI with control byte `$8A`: Port A output, Port B input, Port C lower output, Port C upper input.

**Port C bit assignments:**

| Bit | Direction | Signal |
|---|---|---|
| PC0 | Out | VDG `AG` (alphanumeric / graphics select) |
| PC1 | Out | VDG `GM0` |
| PC2 | Out | VDG `GM1` |
| PC3 | Out | VDG `GM2` |
| PC4 | Out | Sound output (bit-banged square wave) |
| PC5 | Out | Cassette motor relay |
| PC6 | Out | Cassette data out |
| PC7 | In | Cassette data in |

`Ppi8255` exposes `ReadPortA`, `ReadPortB`, `ReadPortC` as `Func<byte>` delegates, allowing peripheral adapters to be wired in without the PPI knowing about them.

### MC6847 VDG — `Mc6847.cs`

The Video Display Generator renders to a 256×192 ARGB32 pixel buffer. It reads video RAM bytes directly via a `byte[]` reference (dual-ported, matching hardware).

Display modes are controlled by `VDG.Control`, set from `Ppi.PortCLatch` each frame:

| AG | GM2 GM1 GM0 | Mode | Resolution | Colours |
|---|---|---|---|---|
| 0 | — | Alphanumeric | 32×16 chars | Green/buff + black |
| 1 | 0 0 0 | CG1 | 64×64 | 4-colour |
| 1 | 0 0 1 | RG1 | 128×64 | 2-colour |
| 1 | 0 1 0 | CG2 | 128×64 | 4-colour |
| 1 | 0 1 1 | RG2 | 128×96 | 2-colour |
| 1 | 1 0 0 | CG3 | 128×96 | 4-colour |
| 1 | 1 0 1 | RG3 | 128×192 | 2-colour |
| 1 | 1 1 0 | CG6 | 128×192 | 4-colour |
| 1 | 1 1 1 | RG6 | 256×192 | 2-colour |

CSS (bit 4) selects the colour palette for each mode. All modes are upscaled to fill the 256×192 output buffer.

The optional character ROM is 768 bytes: 64 characters × 12 rows × 1 byte per row.

### Keyboard — `AtomKeyboardAdapter.cs`

The Atom keyboard is a 10×6 matrix. The OS writes a row number (0–9) to Port A; the adapter returns the active-low column byte on Port B. A key press pulls its column bit to 0.

```
ScanColumns(byte rowSelect) → byte  // wired to Ppi.ReadPortB
```

The adapter takes an `IPhysicalKeyboard` (position-based key queries, e.g. from `RaylibHost`) and maps physical keys to matrix positions. See `AtomKeyboardAdapter._matrix` for the full layout.

### Sound — `AtomSoundAdapter.cs`

The Atom has no dedicated sound chip. PC4 is toggled by software to produce a square wave. `AtomSoundAdapter` records toggle timestamps during a frame and interpolates a 44100 Hz mono PCM buffer at `EndFrame`, which it submits to `IAudioSink`.

| Constant | Value |
|---|---|
| `SampleRate` | 44100 Hz |
| `FrameRate` | 50 Hz |
| `SamplesPerFrame` | 882 |
| `CyclesPerFrame` | 20 000 |

### Tape — `AtomTapeAdapter.cs` + `UefParser.cs`

UEF (Unified Emulator Format) tape images are parsed into a `List<bool>` bit stream by `UefParser.Parse(Stream)`. The parser handles gzip-compressed files transparently.

Supported UEF chunks:

| Chunk ID | Content | Conversion |
|---|---|---|
| `0x0100` | Raw byte data | Each byte → start bit (0) + 8 data bits LSB-first + stop bit (1) |
| `0x0110` | Carrier tone (cycles) | `cycles / 8` high bits |
| `0x0112` | Integer gap (1/20 s units) | `twentieths × 15` high bits |
| `0x0116` | Float gap (seconds) | `seconds × 300` high bits |

`AtomTapeAdapter` streams those bits to the CPU at 300 baud (3333 cycles/bit at 1 MHz). Motor control is cycle-accurate: `MotorOn(cycle)` and `MotorOff(cycle)` freeze/resume the tape position precisely.

The cassette read signal appears on PC7 (active low). The motor relay is driven by PC5. Both are wired automatically when a tape is passed to `AtomMachine`.

---

## Machine compositor — `AtomMachine.cs`

`AtomMachine` wires all components together. Constructor:

```csharp
new AtomMachine(
    byte[] basicRom,
    byte[] osRom,
    IPhysicalKeyboard? keyboard = null,
    IAudioSink?        audio    = null,
    byte[]?            floatRom = null,
    byte[]?            extRom   = null,
    byte[]?            charRom  = null,
    AtomTapeAdapter?   tape     = null)
```

The typical emulator loop:

```csharp
machine.Reset();
while (host.IsRunning)
{
    host.PollEvents();
    machine.RunFrame();        // ~20 000 CPU cycles + audio synthesis
    machine.RenderFrame(host); // VDG renders 256×192 to IVideoSink
}
```

`RunFrame` runs until `TotalCycles` advances by `CyclesPerFrame` (20 000), bracketing audio generation with `BeginFrame`/`EndFrame`. On each `Step`, Port C changes trigger `NotifyPortC` (sound) and `MotorOn`/`MotorOff` (tape).

---

## Platform adapter — `RaylibHost` (Adapters.Raylib)

`RaylibHost` is the single object passed to the machine for all platform I/O. It implements:

| Interface | Responsibility |
|---|---|
| `IVideoSink` | Converts ARGB32 → RGBA32, uploads to GPU texture, draws scaled to window |
| `IAudioSink` | Feeds PCM samples into Raylib's audio stream (drops frame if stream busy) |
| `IPhysicalKeyboard` | Polls Raylib key state via `RaylibKeyMap` lookup |

```csharp
using var host = new RaylibHost("Acorn Atom", scale: 3);
```
