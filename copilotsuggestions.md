# Cpu6502 Architecture and Risk Plans

This document turns the previously identified **risky areas** and **architectural improvements** into concrete implementation plans.

## Risk mitigation plans

### 1) Risk: `AddressDecoder` linear scan per access
**Status:** ✅ Complete  
**Current concern:** `AddressDecoder.ResolveWithBase` does a reverse list scan on every read/write.  
**Goal:** Remove lookup cost growth as machine complexity increases.

**Plan**
1. Add a page-based resolver (`256` pages of `256` bytes) in `AddressDecoder`.
2. Keep `Map(from, to, device)` public API unchanged.
3. Build/refresh page entries at map time; preserve “last mapping wins” behavior.
4. Add tests for overlap precedence and offset translation.
5. Keep old resolver behind an internal switch temporarily for A/B correctness checks.

**Acceptance criteria**
- Existing decoder tests still pass.
- Overlap semantics and open-bus behavior remain unchanged.

---

### 2) Risk: Instruction-granular timing model
**Status:** ✅ Complete  
**Current concern:** Devices advance per instruction, not per bus cycle/event.  
**Goal:** Enable cycle-exact interactions (IRQ edges, tape pulses, contention).

**Plan**
1. Introduce a machine-level timing coordinator (`Clock`/`Scheduler`) abstraction.
2. Add optional cycle callbacks from CPU execution path (without breaking `Step()` API).
3. Route VIA/tape/video timing through scheduler events rather than instruction deltas.
4. Migrate one subsystem first (VIC tape), then IRQ/VBL, then remaining peripherals.
5. Keep frame loop stable (`RunFrame`) while internals become event-driven.

**Acceptance criteria**
- Existing behavior is preserved for current ROM boots.
- New timing tests cover sub-instruction edge timing cases.

---

### 3) Risk: Duplicated ADC/SBC logic paths
**Status:** ✅ Complete  
**Current concern:** Separate arithmetic paths exist in legal and illegal opcode implementations.  
**Goal:** Single arithmetic truth source to reduce divergence risk.

**Plan**
1. Extract shared helpers for ADC/SBC NMOS behavior (including decimal mode handling assumptions).
2. Refactor legal and illegal handlers to call the same helpers.
3. Add targeted tests for carry/overflow/negative/zero edge cases in both paths.
4. Remove duplicate arithmetic code once parity is proven.

**Acceptance criteria**
- No opcode behavior regressions in current tests.
- Illegal opcode arithmetic tests cover parity with legal-path expectations where applicable.

---

### 4) Risk: VIC tape edge loss on long instruction gaps
**Current concern:** Tape ticking may miss multiple pulse transitions if many cycles elapsed.  
**Goal:** Consume all elapsed edges reliably.

**Plan**
1. Update `Vic20TapeAdapter.Tick` to process transitions in a loop until caught up to current cycle.
2. Emit `OnEdge` for every transition in sequence.
3. Add tests where elapsed cycles span multiple pulses in a single call.
4. Verify VIA CB1 edge IRQ behavior remains correct with burst transitions.

**Acceptance criteria**
- Multi-edge elapsed-cycle tests pass.
- No regressions in existing tape/VIA tests.

---

### 5) Risk: Host adapter coupling and noisy diagnostics
**Current concern:** `RaylibHost` depends on Atom-specific constants and logs each keypress.  
**Goal:** Keep host generic and runtime output clean.

**Plan**
1. Replace Atom-specific audio sizing in host with parameterized sizing or sink-driven buffering.
2. Move keypress logging behind an explicit debug flag.
3. Ensure host remains reusable for Atom and VIC without machine-specific assumptions.
4. Add lightweight host-constructor tests for both frame sizes.

**Acceptance criteria**
- No unconditional keyboard spam in normal runs.
- Host compiles/works for both machine targets with no cross-machine constants.

---

### 6) Risk: Documentation and code drift
**Current concern:** Some comments/docs disagree with implementation details.  
**Goal:** Keep docs operationally trustworthy.

**Plan**
1. Audit mismatches in `docs/*.md` and XML comments against actual mappings/behavior.
2. Correct inaccurate address map statements and ownership notes (e.g., VIC BASIC mapping commentary).
3. Add a doc review checklist to PR process for machine map changes.
4. Add focused tests where drift previously occurred.

**Acceptance criteria**
- All known mismatches resolved.
- Future map changes require test and doc updates together.

---

### 7) Risk: Optional integration tests can pass vacuously
**Current concern:** Missing external test ROM can still produce green builds.  
**Goal:** Preserve developer convenience while strengthening CI confidence.

**Plan**
1. Keep local default permissive behavior.
2. Add CI mode flag/environment switch that fails if required integration assets are missing.
3. Document asset setup in README and test project notes.
4. Add CI pipeline check for required ROM/test-data presence.

**Acceptance criteria**
- Local dev remains frictionless.
- CI fails on missing integration asset prerequisites.

---

## Architectural improvement implementation plans

### A) Unified timing scheduler (high priority)
**Scope:** CPU/device timing backbone for cycle/event orchestration.

**Plan**
1. Define scheduler interfaces (`AdvanceCycles`, `ScheduleAt`, `Now`).
2. Integrate scheduler with machine `Step`/`RunFrame`.
3. Move peripherals incrementally (tape → VIA IRQ edges → VBL/video timing).
4. Add regression tests around frame-cycle totals and interrupt behavior.

---

### B) O(1) page-table address decoding (high priority)
**Scope:** Decoder internals only, public API stable.

**Plan**
1. Represent each 256-byte page with `(device, base)` entries.
2. On map, update affected pages in order to preserve last-map-wins.
3. Handle partial-page ranges correctly via per-access base offset math.
4. Retain open-bus/unmapped write semantics.

---

### C) Unified arithmetic core + illegal opcode audit (high priority)
**Scope:** CPU internals and opcode correctness.

**Plan**
1. Consolidate ADC/SBC internals.
2. Audit undocumented opcodes for addressing and flag correctness.
3. Add dedicated tests for known fragile opcodes (e.g., SHA/TAS/ARR/ISB/RRA families).
4. Lock down with cycle-count assertions where relevant.

---

### D) Multi-edge tape processing (high priority)
**Scope:** VIC tape and VIA interaction correctness.

**Plan**
1. Implement catch-up edge loop in tape adapter.
2. Stress test with synthetic pulse trains and large cycle jumps.
3. Validate IRQ-triggering edge direction via PCR configuration tests.

---

### E) Host decoupling and runtime cleanliness (medium priority)
**Scope:** `Adapters.Raylib` boundary hygiene.

**Plan**
1. Remove machine-specific constants from generic host code.
2. Add configurable diagnostics mode.
3. Keep constructor/API backward-compatible where practical.

---

### F) Integration and boot-validation hardening (medium priority)
**Scope:** confidence in machine bring-up and CPU conformance.

**Plan**
1. Add stricter CI profile for integration assets.
2. Add machine boot invariants (known vectors/memory signatures).
3. Improve tests that currently only smoke-check.

---

## Suggested execution order

1. Page-table decoder + tape multi-edge fixes (contained, fast wins).  
2. Arithmetic unification + illegal opcode audit (correctness hardening).  
3. Timing scheduler foundation and phased peripheral migration.  
4. Host decoupling, docs drift cleanup, and CI/integration hardening.

