# VIC-20 Tape Adapter

## TAP file format

Commodore TAP is the standard cassette image format for VIC-20 and C64.

**Header (20 bytes):**

| Offset | Size | Value |
|--------|------|-------|
| 0      | 12   | `C64-TAPE-RAW` (ASCII, no null) |
| 12     | 1    | Version (0 or 1) |
| 13     | 3    | Reserved (ignored) |
| 16     | 4    | Data length in bytes (LE uint32) |

**Data bytes (version 0):**

Each byte encodes one pulse width: `cycles = byte × 8` (VIC-20 clock cycles).

**Data bytes (version 1):**

- Non-zero byte: same as version 0 (`cycles = byte × 8`)
- Zero byte: 24-bit LE extended count follows in next 3 bytes — use that value directly as cycle count

---

## TapParser

`src/Machines.Vic20/TapParser.cs`

```csharp
public static class TapParser
{
    // Returns pulse widths in VIC-20 clock cycles (1,108,405 Hz).
    public static int[] Parse(Stream stream);
}
```

Steps:
1. Read 20-byte header; validate magic `C64-TAPE-RAW`; extract version and data length
2. Read `length` bytes
3. Decode per version rules into `int[]` of pulse widths

---

## Vic20TapeAdapter

`src/Machines.Vic20/Vic20TapeAdapter.cs`

```csharp
public sealed class Vic20TapeAdapter
{
    public bool SignalLevel { get; private set; }       // current tape output level
    public Action<bool>? OnEdge { get; set; }           // fires on each level transition

    public void LoadTap(Stream stream);                 // parses and loads pulses
    public void Load(int[] pulses);                     // direct load (for tests)

    public void SetMotor(bool on, ulong currentCycle);  // motor relay, cycle-accurate
    public bool Tick(ulong currentCycle);               // advance; returns true on edge
}
```

Internal state:
- `int[] _pulses` — pulse widths in cycles
- `int _pulseIndex` — current position
- `ulong _nextEdgeAt` — absolute cycle when next edge fires
- `bool _motorOn`, `ulong _motorOnAt`, `int _posAtMotorOn` — motor freeze/resume

`Tick` logic:
- If motor is off, return false
- If `currentCycle >= _nextEdgeAt`: flip `SignalLevel`, invoke `OnEdge`, advance index, schedule next edge
- Return true only when an edge occurred this call

---

## Via6522 additions

Two new methods are needed to deliver tape edges to the VIA:

```csharp
// Sets the CB1 pin level; fires IFR bit 4 on the active edge (PCR bit 4: 0=falling, 1=rising).
public void SetCB1(bool level);

// Sets the CA1 pin level; fires IFR bit 1 on the active edge (PCR bit 0: 0=falling, 1=rising).
public void SetCA1(bool level);
```

Both follow the same pattern:
1. Determine active edge direction from PCR
2. If transition matches active edge → set IFR bit, raise `Irq` if IER bit is set
3. Store new level

---

## Machine wiring

In `Vic20Machine.Step()` (or equivalent per-cycle hook):

```csharp
if (tape is not null && tape.Tick(Cpu.TotalCycles))
    via1.SetCB1(tape.SignalLevel);
```

VIA 1 CB1 is the standard tape-read input on the VIC-20. The motor relay is driven by a VIA output bit; the machine should call `tape.SetMotor(on, Cpu.TotalCycles)` when that bit changes.

---

## Planned tests

| File | Coverage |
|------|----------|
| `TapParserTests.cs` | Header validation; version-0 decoding; version-1 extended counts; mixed |
| `Vic20TapeAdapterTests.cs` | Motor off → no edges; motor on → edges advance with cycles; SetMotor freezes/resumes position |
| `Via6522` edge tests (extend existing) | SetCB1 falling/rising edge sets IFR bit 4 per PCR; SetCA1 sets IFR bit 1 |
