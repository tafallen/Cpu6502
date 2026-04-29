# Acorn Electron — hardware reference

The Acorn Electron (1983) is a cost-reduced BBC Micro. The main cost saving was combining the BBC's separate video, RAM timing, and I/O chips into a single custom VLSI chip: the **ULA** (Uncommitted Logic Array). Understanding the ULA is the key to emulating the Electron.

---

## Address map

| Range | Device |
|---|---|
| `$0000–$7FFF` | RAM — 32 KB |
| `$8000–$BFFF` | Paged ROM — 16 KB (bank selected by ULA) |
| `$C000–$FBFF` | OS ROM — lower 16 KB minus top 1 KB |
| `$FC00–$FEFF` | ULA registers (MMIO) |
| `$FF00–$FFFF` | OS ROM — top 256 bytes (interrupt vectors live here) |

The paged ROM window and the OS ROM are both on the CPU bus simultaneously. The ULA controls which physical ROM chip drives `$8000–$BFFF` based on its ROM page register. `$C000–$FFFF` is always the OS ROM (16 KB, split by the ULA MMIO window).

**Interrupt vectors** (in OS ROM):

| Address | Vector |
|---|---|
| `$FFFA/$FFFB` | NMI |
| `$FFFC/$FFFD` | RESET |
| `$FFFE/$FFFF` | IRQ / BRK |

**MOS entry points** (indirect JMP stubs in OS ROM):

| Address | Name | Description |
|---|---|---|
| `$FFEE` | `OSWRCH` | Write character to current output stream |
| `$FFF1` | `OSRDCH` | Read character from keyboard |
| `$FFF4` | `OSWORD` | Miscellaneous OS operation (A = reason code) |
| `$FFF7` | `OSBYTE` | Single-byte OS operation (A = reason code) |
| `$FFDA` | `OSFILE` | File I/O |

---

## ROM paging

The ULA's ROM page register (bottom 4 bits of `$FE05`) selects which 16 KB bank occupies `$8000–$BFFF`.

| Page value | ROM |
|---|---|
| 0–3 | External cartridge slot (not populated on base machine) |
| 4–7 | Second cartridge slot (not populated on base machine) |
| 8 | Keyboard interrupt handler / OS extension (built-in) |
| 9 | OS extension (built-in) |
| 10 | BBC BASIC II — lower 8 KB (built-in) |
| 11 | BBC BASIC II — upper 8 KB (built-in) |
| 12–15 | Not used (map to open bus or mirrors) |

On an unexpanded machine only pages 8–11 are populated. The OS boots with BASIC paged in (page 10/11 overlap to present 16 KB of BASIC at `$8000`).

---

## ULA — `$FC00–$FEFF`

The ULA is partially decoded: only the lowest two address bits are significant within its range, so `$FC00`, `$FC04`, `$FC08`, … all hit register 0; `$FC01`, `$FC05`, … all hit register 1; and so on.

| Offset | Read | Write |
|---|---|---|
| `+0` (`$FE00`) | Interrupt status register | Interrupt clear / enable register |
| `+4` (`$FE04`) | Cassette data shift register | Cassette data shift register |
| `+5` (`$FE05`) | (undefined) | High nibble: interrupt enable mask; low nibble: ROM page select |
| `+7` (`$FE07`) | Keyboard / cassette data-in | Cassette tone + motor control |

### Interrupt status register (`$FE00` read)

Bit 7 is the master interrupt flag (OR of all enabled, pending interrupts). The OS IRQ handler checks bit 7 first to confirm the ULA caused the interrupt.

| Bit | Signal |
|---|---|
| 7 | Master interrupt (any enabled interrupt pending) |
| 6 | Power-on reset flag (cleared after boot) |
| 5 | `/RDY` — cassette input edge detected |
| 4 | `/RTC` — 100 Hz real-time clock tick |
| 3 | Display end (end of active display, used as vsync) |
| 2 | `/DISP` — display start (fires at top of frame) |
| 1 | Cassette transmit empty |
| 0 | Cassette receive full |

### Interrupt enable/clear (`$FE00` write, `$FE05` high nibble write)

Writing to `$FE00` clears the bits set in the written value. Writing to the high nibble of `$FE05` sets the interrupt enable mask (bit N set = enable interrupt N).

### ROM page register (`$FE05` low nibble write)

Writing bits [3:0] to `$FE05` selects the active ROM bank (see table above).

### Keyboard scan (`$FE07` read)

Reading `$FE07` with the keyboard column driven on certain address pins returns the row state. The ULA scans a 14-column × 4-row matrix internally. The OS keyboard handler is driven by the 100 Hz RTC interrupt — it cycles through columns writing the column index to the address bus while reading `$FE07`.

The exact addressing trick: to scan column *N*, the OS reads address `$FE00 | (N << 1)` (i.e. `$FE00`, `$FE02`, `$FE04`, … `$FE1A`). The ULA latches the column from the address lines rather than the data bus.

| Bit | Key (row) |
|---|---|
| 0 | Row 0 |
| 1 | Row 1 |
| 2 | Row 2 |
| 3 | Row 3 |
| 4–7 | Unused / open |

### Cassette / motor control (`$FE07` write)

| Bit | Signal |
|---|---|
| 7 | Cassette motor relay (1 = motor on) |
| 6–1 | Tone frequency divisor (bit-banged carrier) |
| 0 | Cassette transmit data bit |

---

## Video

The ULA generates the display directly from RAM without a dedicated framebuffer chip. The screen memory starts near the top of RAM and grows **downward** by mode.

| Mode | Type | Resolution | Colours | RAM needed | Screen start |
|---|---|---|---|---|---|
| 0 | Graphics | 640×256 | 2 | 20 KB | `$3000` |
| 1 | Graphics | 320×256 | 4 | 20 KB | `$3000` |
| 2 | Graphics | 160×256 | 16 | 20 KB | `$3000` |
| 3 | Text | 640×200 | 2 | 16 KB | `$4000` |
| 4 | Graphics | 320×256 | 2 | 10 KB | `$5800` |
| 5 | Graphics | 160×256 | 4 | 10 KB | `$5800` |
| 6 | Text | 320×200 | 2 | 8 KB | `$6000` |

The screen memory address is hardwired per mode. The ULA reads bytes sequentially; the OS writes graphics using the standard MOS `PLOT`/`DRAW` routines via OSWRCH. No separate character ROM is used — the OS ROM contains the 8×8 font at a fixed address.

**Timing:** PAL, 50 Hz. The `Display end` interrupt (bit 3) fires at the end of the active display area and is the primary sync signal for the OS cursor blink and screen scroll logic.

---

## Sound

The Electron has no dedicated sound chip. Sound is produced by toggling bit 0 of `$FE07` to generate a square wave through the cassette port speaker. This is bit-banged by software — the OS provides SOUND/ENVELOPE commands that schedule toggle times at a 1 MHz timer resolution.

---

## CPU

Standard MOS 6502 (NMOS), running at **2 MHz** (1.79 MHz effective due to RAM contention — the ULA steals cycles for display DMA during active scan). Clock contention is mode-dependent:

| Mode | Effective CPU speed |
|---|---|
| 0–2 (20 KB modes) | ~1 MHz during display |
| 3–6 | ~1.5–1.8 MHz during display |
| All | 2 MHz during blanking |

For a first-pass emulator, run the CPU at a constant 2 MHz (2 000 000 / 50 = 40 000 cycles per frame) and fire a `Nmi()` at the end of each frame for vertical blank. Cycle-stealing can be added later.

---

## Emulator skeleton

```csharp
public class ElectronMachine
{
    public Cpu  Cpu  { get; }
    public Ram  Ram  { get; }
    public ElectronUla Ula { get; }   // IBus implementation

    private const int CyclesPerFrame = 40_000;  // 2 MHz / 50 Hz

    public ElectronMachine(byte[] osRom, byte[] basicRom)
    {
        Ram = new Ram(0x8000);   // 32 KB RAM at $0000–$7FFF
        Ula = new ElectronUla(osRom, basicRom);

        var bus = new AddressDecoder();
        bus.Map(0x0000, 0x7FFF, Ram);
        bus.Map(0x8000, 0xFFFF, Ula);  // ULA owns the whole upper half:
                                       // presents paged ROM, OS ROM, and MMIO

        Cpu = new Cpu(bus);
    }

    public void Reset() => Cpu.Reset();

    public void RunFrame()
    {
        ulong target = Cpu.TotalCycles + CyclesPerFrame;
        while (Cpu.TotalCycles < target)
            Cpu.Step();
        Cpu.Nmi();  // end-of-frame vertical blank
    }
}
```

`ElectronUla.Read(address)` must decode the full `$8000–$FFFF` range:
- `$8000–$BFFF` → return current paged ROM byte
- `$C000–$FBFF` and `$FF00–$FFFF` → return OS ROM byte
- `$FC00–$FEFF` → return ULA register value

`ElectronUla.Write(address, value)` handles:
- `$FE00` → clear interrupt flags
- `$FE04` → cassette shift register
- `$FE05` → interrupt enable (high nibble) + ROM page (low nibble)
- `$FE07` → cassette motor + tone (high nibble) + transmit bit (low nibble)

---

## Differences from Acorn Atom

| Feature | Atom | Electron |
|---|---|---|
| CPU speed | 1 MHz | 2 MHz (with contention) |
| Video chip | MC6847 VDG (separate chip) | ULA (integrated) |
| Video RAM | Dedicated 8 KB ($8000–$9FFF) | Shared with main RAM |
| ROM banking | Fixed 4 KB slots | 16 KB paged window |
| Sound | PPI PC4 bit-bang | $FE07 bit-bang |
| Keyboard | 8255 PPI matrix | ULA address-line scan |
| I/O hub | Intel 8255 PPI | Custom ULA |
| Tape format | UEF (300 baud) | UEF (1200 baud) |
| Interrupt source | MC6847 /FS → IRQ | ULA RTC/display → IRQ + VBL → NMI |
