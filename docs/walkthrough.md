# Using the Cpu6502 emulator — walkthrough

This guide builds up from running a single instruction to wiring together a complete machine with ROM, RAM, and a custom peripheral.

---

## 1. The bus interface

Everything the CPU reads or writes goes through `IBus`:

```csharp
public interface IBus
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
}
```

The CPU never holds a reference to RAM, ROM, or any peripheral directly. It only ever calls `Read` and `Write`. This means any combination of hardware can be presented to the CPU by implementing `IBus`.

---

## 2. Running your first program

The simplest possible setup uses a single `Ram` as the entire address space.

```csharp
var ram = new Ram(0x10000);   // 64 KB

// Write a short program at $0200
ram.Load(0x0200, new byte[]
{
    0xA9, 0x00,   // LDA #$00   clear A
    0xA2, 0x05,   // LDX #$05   loop counter = 5
    0x18,         // CLC
    0x69, 0x01,   // ADC #$01   A = A + 1
    0xCA,         // DEX
    0xD0, 0xFB,   // BNE -5     loop back to ADC
    0x00          // BRK        halt
});

// Write the RESET vector ($FFFC/$FFFD) — little-endian
ram.Write(0xFFFC, 0x00);
ram.Write(0xFFFD, 0x02);   // PC starts at $0200

var cpu = new Cpu(ram);
cpu.Reset();

// Step until BRK
ushort prev = cpu.PC;
do
{
    prev = cpu.PC;
    cpu.Step();
} while (cpu.PC != prev);   // BRK traps at its own address (loads IRQ vector $0000 here)

Console.WriteLine(cpu.A);          // → 5
Console.WriteLine(cpu.TotalCycles); // exact cycle count for every instruction
```

### What Reset() does

`Reset()` reads the two bytes at `$FFFC`/`$FFFD` as a little-endian address and sets PC to that value. It also sets SP=`$FD`, sets the interrupt-disable flag, and clears the decimal flag — exactly what real 6502 hardware does. It costs 7 cycles.

---

## 3. Inspecting CPU state

All registers and flags are public read-only properties:

```csharp
cpu.A   // accumulator
cpu.X   // X index register
cpu.Y   // Y index register
cpu.SP  // stack pointer (page 1: $0100–$01FF)
cpu.PC  // program counter

cpu.C   // carry
cpu.Z   // zero
cpu.N   // negative
cpu.V   // overflow
cpu.I   // interrupt disable
cpu.D   // decimal mode

cpu.TotalCycles   // ulong — every cycle ever consumed, including Reset()

// Get the processor status byte (what PHP pushes / BRK pushes)
byte p = cpu.GetStatus();              // B flag = 0
byte p = cpu.GetStatus(breakFlag: true); // B flag = 1, as pushed by PHP/BRK
```

---

## 4. Composing a machine with AddressDecoder

Real machines split their 64 KB address space between RAM, ROM, and memory-mapped I/O registers. `AddressDecoder` handles this by mapping address ranges to `IBus` implementations.

```csharp
var bus = new AddressDecoder();

// RAM occupies the lower 32 KB
bus.Map(0x0000, 0x7FFF, new Ram(0x8000));

// Two ROM images occupy the upper 32 KB
bus.Map(0x8000, 0xBFFF, new Rom(File.ReadAllBytes("basic.rom")));
bus.Map(0xC000, 0xFFFF, new Rom(File.ReadAllBytes("os.rom")));

var cpu = new Cpu(bus);
cpu.Reset();   // reads RESET vector from whatever is mapped at $FFFC/$FFFD
```

### Mapping rules

- Ranges are **inclusive** on both ends: `Map(0x8000, 0xFFFF, ...)` covers `$8000` through `$FFFF`.
- **Last registration wins** on overlap. Map a broad range first, then narrow MMIO ranges on top of it.
- Unmapped reads return `$FF` (open bus). Unmapped writes are silently discarded.

```csharp
// Broad RAM first, then a narrow MMIO window on top
bus.Map(0x0000, 0xFFFF, new Ram(0x10000));   // whole space is RAM
bus.Map(0xFE00, 0xFEFF, new MyUla());        // ULA registers override $FE00–$FEFF
```

---

## 5. Implementing a peripheral

Any chip that responds to CPU reads or writes implements `IBus`. Here is a minimal UART that the CPU can write characters to:

```csharp
public class Uart : IBus
{
    // $FE00 = transmit register (write-only from CPU's perspective)
    public event Action<char>? CharacterReceived;

    public byte Read(ushort address) => 0xFF;   // no readable state exposed here

    public void Write(ushort address, byte value)
    {
        if (address == 0xFE00)
            CharacterReceived?.Invoke((char)value);
    }
}
```

Wire it in:

```csharp
var uart = new Uart();
uart.CharacterReceived += c => Console.Write(c);

bus.Map(0xFE00, 0xFE0F, uart);
```

Now any `STA $FE00` instruction in the ROM will fire the event. The CPU has no knowledge of the UART — it just writes to an address.

---

## 6. Handling interrupts

### IRQ (maskable)

`cpu.Irq()` sets a pending flag. The CPU checks it before executing the next instruction. If the interrupt-disable flag (`I`) is set, the IRQ is ignored. When serviced, the CPU:

1. Pushes PC (hi, then lo) onto the stack
2. Pushes the status byte (B=0)
3. Sets I=1
4. Loads PC from the IRQ/BRK vector at `$FFFE`/`$FFFF`

```csharp
// In your peripheral, when the hardware wants to interrupt:
cpu.Irq();

// The ROM's IRQ handler must end with RTI to return
```

### NMI (non-maskable)

`cpu.Nmi()` is never masked. Same sequence as IRQ but uses the NMI vector at `$FFFA`/`$FFFB`. Commonly used for the vertical blank interrupt on video hardware.

```csharp
cpu.Nmi();   // fires unconditionally, even if I=1
```

### Vectors summary

| Vector | Address | Triggered by |
|--------|---------|--------------|
| NMI | `$FFFA`/`$FFFB` | `cpu.Nmi()` |
| RESET | `$FFFC`/`$FFFD` | `cpu.Reset()` |
| IRQ/BRK | `$FFFE`/`$FFFF` | `cpu.Irq()` or BRK instruction |

The addresses are read from whatever `IBus` implementation is mapped there — typically a ROM image that has the correct vector table baked in at the top of its address range.

---

## 7. Running at speed

`Step()` executes exactly one instruction and returns. For a real machine you typically want to run a fixed number of cycles per video frame and then render. The `TotalCycles` counter tells you how far you have advanced:

```csharp
const ulong CyclesPerFrame = 2_000_000 / 50;   // 2 MHz PAL, 50 Hz

ulong frameStart = cpu.TotalCycles;
while (cpu.TotalCycles - frameStart < CyclesPerFrame)
    cpu.Step();

RenderFrame();
```

For a faster tight loop without the overhead of a boundary check on every instruction, batch by instruction count instead and accept that individual frames will not be perfectly timed.

---

## 8. Putting it together — Acorn Electron skeleton

This is the shape of a minimal Electron machine definition. The peripheral implementations are left as stubs.

```csharp
public class ElectronMachine
{
    private readonly Cpu    _cpu;
    private readonly Ram    _ram;
    private readonly Ula    _ula;      // your IBus for $FE00–$FEFF

    public ElectronMachine(byte[] osRom, byte[] basicRom)
    {
        _ram = new Ram(0x8000);   // 32 KB RAM, $0000–$7FFF
        _ula = new Ula();

        var bus = new AddressDecoder();
        bus.Map(0x0000, 0x7FFF, _ram);
        bus.Map(0x8000, 0xBFFF, new Rom(basicRom));
        bus.Map(0xC000, 0xFFFF, new Rom(osRom));
        bus.Map(0xFE00, 0xFEFF, _ula);   // ULA MMIO overlaps the OS ROM window

        _cpu = new Cpu(bus);
    }

    public void PowerOn()
    {
        _cpu.Reset();
    }

    public void RunFrame()
    {
        const ulong cyclesPerFrame = 2_000_000 / 50;
        ulong start = _cpu.TotalCycles;
        while (_cpu.TotalCycles - start < cyclesPerFrame)
            _cpu.Step();

        // Signal vertical blank — fires NMI on the Electron
        _cpu.Nmi();
    }
}
```

With `osRom` and `basicRom` set to the real ROM images, `PowerOn()` followed by repeated `RunFrame()` calls will drive the CPU through the OS boot sequence at the correct speed.
