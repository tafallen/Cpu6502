# Architectural Analysis: Cpu6502 Emulator
**Date:** April 25, 2026
**Analyst:** Gemini CLI

## Executive Summary
The Cpu6502 project is a highly modular, cycle-accurate implementation of the MOS 6502 processor. Its architecture excels in the separation of concerns, using a composition-based approach to build specific machine emulators (e.g., Acorn Atom) from machine-agnostic components.

---

## 1. Architectural Strengths

### 1.1 Composition over Inheritance
The project avoids a rigid class hierarchy. The `Cpu` class is entirely unaware of the machine it resides in, interacting only with an `IBus` interface. Machines are "built" at runtime by mapping address ranges to devices using the `AddressDecoder`.

### 1.2 Instruction-Level Granularity
The use of `partial` classes to group instructions (Arithmetic, Logic, Branch, etc.) is an excellent maintainability choice. It prevents `Cpu.cs` from becoming a massive, unreadable file while keeping the 256-opcode dispatch table logic centralized.

### 1.3 Cycle Accuracy
The implementation correctly handles subtle hardware quirks that many high-level emulators miss:
*   **Page-Crossing Penalties:** Correctly applied in indexing modes.
*   **Indirect JMP Bug:** Implements the classic page-wrap behavior of the original NMOS 6502.
*   **BCD Mode:** Accurate Binary Coded Decimal logic for `ADC` and `SBC`.

### 1.4 Abstraction of I/O
The `IAudioSink` and `IVideoSink` interfaces in `Machines.Common` ensure that the emulator's core logic is decoupled from UI frameworks like Raylib, allowing for headless testing or porting to other platforms.

---

## 2. Identified Weaknesses

### 2.1 Memory Access Performance
The `AddressDecoder` currently uses a `List<Mapping>` and iterates through it for every memory access ($O(N)$). While acceptable for a 1-2MHz emulator, this becomes a bottleneck for higher-speed emulation or more complex machine definitions.

### 2.2 Delegate Dispatch Overhead
The `Action[] _ops` table is elegant but incurs a delegate invocation cost for every instruction. This is slightly less performant than a native jump table or a large, JIT-optimized `switch` statement.

### 2.3 Instruction-Level Stepping
The emulator steps one full instruction at a time. This makes it difficult to emulate "mid-instruction" hardware interactions (e.g., a video chip being updated exactly 3 cycles into a 6-cycle instruction), which is required for some advanced 8-bit software tricks.

---

## 3. Recommended Enhancements

### 3.1 Page-Table Address Decoding
**Recommendation:** Replace the `List` in `AddressDecoder` with a page-table array of 256 pointers (each representing 256 bytes of address space).
*   **Impact:** Reduces memory resolution to $O(1)$, significantly improving performance during high-volume memory operations.

### 3.2 Switch-Based Dispatch
**Recommendation:** Transition the `Step()` method to use a single large `switch (opcode)` block.
*   **Impact:** Allows the .NET JIT compiler to optimize the dispatch into a hardware-level jump table, reducing per-instruction overhead.

### 3.3 State Serialization (Save States)
**Recommendation:** Implement a unified state capture interface across the CPU and all `IBus` devices.
*   **Impact:** Enables "Save State" and "Rewind" functionality, essential for modern emulation user experiences.

### 3.4 Bus-Centric Timing
**Recommendation:** Centralize cycle counting within the `Read` and `Write` methods of the CPU's bus interface.
*   **Impact:** Makes the emulator "Cycle-Exact" rather than just "Cycle-Accurate," allowing it to handle wait states and cycle-stealing peripherals more naturally.
