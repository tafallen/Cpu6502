# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run all tests (includes coverage enforcement — fails if line coverage < 80%)
dotnet test

# Run a single test class
dotnet test --filter "ClassName=Cpu6502.Tests.ArithmeticTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName=Cpu6502.Tests.ArithmeticTests.ADC_Immediate_AddsWithoutCarry"

# Run tests without coverage (faster during development)
dotnet test /p:CollectCoverage=false
```

Coverage reports are written to `tests/Cpu6502.Tests/coverage/coverage.cobertura.xml` after each `dotnet test` run.

## Architecture

The goal is to run real 80s ROM images by composing hardware components into machine definitions. The CPU knows nothing about the machine it is in — it only talks to `IBus`.

### Hardware composition pattern

```
Cpu  →  IBus
              AddressDecoder  (routes by address range)
                ├── Ram       ($0000–$7FFF)
                ├── Rom       ($8000–$BFFF)  ← load ROM image bytes here
                └── Peripheral (IBus)        ← ULA, VIA, etc. added later
```

`AddressDecoder.Map(from, to, device)` registers a component for an inclusive address range. Last registration wins on overlap. Unmapped reads return `0xFF` (open bus); unmapped writes are silent. This is how a machine definition is assembled — there is no "machine" class, just a configured `AddressDecoder`.

### CPU implementation

`Cpu` is a `sealed partial` class split across files by instruction group:

| File | Content |
|---|---|
| `Cpu.cs` | Registers, flags, `Reset`/`Step`/`Irq`/`Nmi`, status byte helpers, dispatch table builder |
| `Cpu.AddressingModes.cs` | All 11 addressing modes as `private` methods returning `ushort` effective address |
| `Cpu.LoadStore.cs` | LDA/LDX/LDY/STA/STX/STY |
| `Cpu.Arithmetic.cs` | ADC/SBC/INC/DEC/INX/INY/DEX/DEY (includes BCD mode) |
| `Cpu.Logic.cs` | AND/ORA/EOR/BIT |
| `Cpu.Shifts.cs` | ASL/LSR/ROL/ROR |
| `Cpu.Compare.cs` | CMP/CPX/CPY |
| `Cpu.Branches.cs` | BCC/BCS/BEQ/BNE/BMI/BPL/BVC/BVS |
| `Cpu.Jumps.cs` | JMP/JSR/RTS/BRK/RTI |
| `Cpu.Transfer.cs` | TAX/TAY/TXA/TYA/TSX/TXS/PHA/PLA/PHP/PLP |
| `Cpu.Flags.cs` | CLC/SEC/CLI/SEI/CLD/SED/CLV/NOP |

Opcodes are dispatched via `Action[] _ops` (256 slots). Illegal opcodes throw `InvalidOperationException`. Each instruction method adds its own base cycle count directly to `TotalCycles`; the addressing mode helpers add the page-cross `+1` penalty inline.

### Cycle counting rule for write and RMW instructions

Read instructions pay `+1` only when a page boundary is crossed (`AddrAbsoluteX()` / `AddrAbsoluteY()` default). Write and read-modify-write instructions **always** pay the same cycle count regardless of page crossing — pass no `alwaysAddCycle` argument and bake the full count into the base (`TotalCycles += 5` for STA abs,X; `TotalCycles += 7` for INC abs,X). Using `alwaysAddCycle: true` adds an *extra* cycle on top, which is wrong for these cases.

### Test pattern

All CPU tests inherit `CpuFixture`, which wires a 64 KB `Ram` as the bus. Tests write opcodes into RAM at a known address, call `Load()` to set PC via the reset vector, then `Step(n)` to execute instructions.

```csharp
Load(0x0200, 0xA9, 0x42);  // LDA #$42
Step();
Assert.Equal(0x42, Cpu.A);
Assert.Equal(2UL, Cpu.TotalCycles - before);  // always assert cycle count too
```

Every test asserts **both** the observable state (registers/flags/memory) **and** the cycle count.

### Klaus Dörmann integration test

`IntegrationTests.cs` runs the industry-standard 6502 functional test suite. Download `6502_functional_test.bin` from https://github.com/Klaus2m5/6502_65C02_functional_tests and place it in `tests/Cpu6502.Tests/TestData/`. The test loads the binary at `$0000`, sets PC to `$0400`, and asserts the CPU reaches `$3469` (success). If absent the test passes vacuously.

### 6502 hardware quirks implemented

- **Indirect JMP page-wrap bug**: `JMP ($10FF)` reads hi byte from `$1000`, not `$1100` — implemented in `ReadWordBug()`.
- **JSR pushes PC−1**: the return address on the stack is the last byte of the JSR instruction, not the next instruction. RTS adds 1 on the way back.
- **BRK skips the padding byte**: BRK is effectively a 2-byte instruction; RTI returns to `PC+2` of the BRK.
- **Zero-page indexed addressing wraps**: `$FF + X=1` → `$00`, not `$0100`.
- **BCD mode**: implemented in `DoADC` / `DoSBC`; overflow flag is undefined in BCD on NMOS 6502 and is not set.
