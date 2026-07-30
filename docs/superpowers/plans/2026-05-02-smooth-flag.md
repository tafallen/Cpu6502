# --smooth / --scanlines Display Flags — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add full test coverage for the `--smooth` and `--scanlines` display flags, which are already fully implemented but untested.

**Architecture:** The feature exists across four layers — `DisplayOptions` (config record), `RaylibHost` (applies config, handles hotkeys), and the two command-line parsers (`AtomCommandLine`, `Vic20CommandLine`). Each layer gets its own test task. A final task removes the feature from the Future Work list in `CLAUDE.md`.

**Tech Stack:** C# / xUnit / `FakeRaylibBackend` (already exists in `RaylibHostTests.cs`)

---

## Current state

The following is already fully implemented and shipping:

| File | What it does |
|---|---|
| `src/Adapters.Raylib/DisplayOptions.cs` | `Scale`, `Smooth`, `ScanlineIntensity` with validation |
| `src/Adapters.Raylib/RaylibHost.cs` | Applies bilinear filter on init; applies scanlines in `SubmitFrame`; F10 toggles smooth, F11 cycles scanline intensity; OSD overlay shows current state |
| `src/Host.Atom/AtomCommandLine.cs` | Parses `--smooth` (bool flag) and `--scanlines <0..1>` |
| `src/Host.Vic20/Vic20CommandLine.cs` | Same |

**What's missing:** zero tests cover any of this. The plan below adds them.

---

## File map

| Action | File |
|---|---|
| **Create** | `tests/Machines.Atom.Tests/DisplayOptionsTests.cs` |
| **Modify** | `tests/Machines.Atom.Tests/RaylibHostTests.cs` — extend `FakeRaylibBackend`, add display tests |
| **Modify** | `tests/Machines.Atom.Tests/AtomCommandLineTests.cs` — add smooth/scanlines cases |
| **Modify** | `tests/Machines.Vic20.Tests/Vic20CommandLineTests.cs` — add smooth/scanlines cases |
| **Modify** | `CLAUDE.md` — remove `--smooth` and `--scanlines` from the Future Work section |

---

### Task 1: `DisplayOptions` unit tests

**Files:**
- Create: `tests/Machines.Atom.Tests/DisplayOptionsTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Machines.Atom.Tests/DisplayOptionsTests.cs
using Adapters.Raylib;

namespace Machines.Atom.Tests;

public class DisplayOptionsTests
{
    [Fact]
    public void DefaultConstructor_HasExpectedDefaults()
    {
        var opts = new DisplayOptions();
        Assert.Equal(3, opts.Scale);
        Assert.False(opts.Smooth);
        Assert.Equal(0f, opts.ScanlineIntensity);
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var opts = new DisplayOptions(Scale: 4, Smooth: true, ScanlineIntensity: 0.5f);
        Assert.Equal(4, opts.Scale);
        Assert.True(opts.Smooth);
        Assert.Equal(0.5f, opts.ScanlineIntensity);
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    public void Constructor_InvalidScanlineIntensity_Throws(float intensity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DisplayOptions(ScanlineIntensity: intensity));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void Constructor_ValidBoundaryScanlineIntensity_DoesNotThrow(float intensity)
    {
        var opts = new DisplayOptions(ScanlineIntensity: intensity);
        Assert.Equal(intensity, opts.ScanlineIntensity);
    }

    [Fact]
    public void IsValid_ReturnsTrueForValidOptions()
    {
        Assert.True(new DisplayOptions(ScanlineIntensity: 0.5f).IsValid);
    }

    [Fact]
    public void WithSmooth_ReturnsNewInstanceWithSmoothSet()
    {
        var original = new DisplayOptions(Scale: 2, Smooth: false, ScanlineIntensity: 0.3f);
        var updated  = original.WithSmooth(true);
        Assert.True(updated.Smooth);
        Assert.Equal(2, updated.Scale);           // unchanged
        Assert.Equal(0.3f, updated.ScanlineIntensity); // unchanged
        Assert.False(original.Smooth);            // original unmodified
    }

    [Fact]
    public void WithScanlines_ReturnsNewInstanceWithIntensitySet()
    {
        var original = new DisplayOptions(Scale: 2, Smooth: true, ScanlineIntensity: 0f);
        var updated  = original.WithScanlines(0.5f);
        Assert.Equal(0.5f, updated.ScanlineIntensity);
        Assert.Equal(2, updated.Scale);   // unchanged
        Assert.True(updated.Smooth);      // unchanged
        Assert.Equal(0f, original.ScanlineIntensity); // original unmodified
    }

    [Fact]
    public void SetScanlineIntensity_UpdatesMutableProperty()
    {
        var opts = new DisplayOptions();
        opts.SetScanlineIntensity(0.4f);
        Assert.Equal(0.4f, opts.ScanlineIntensity);
    }

    [Fact]
    public void SetScanlineIntensity_OutOfRange_Throws()
    {
        var opts = new DisplayOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => opts.SetScanlineIntensity(1.5f));
    }

    [Fact]
    public void WithScale_ReturnsNewInstanceWithScaleChanged()
    {
        var original = new DisplayOptions(Scale: 3, Smooth: true, ScanlineIntensity: 0.3f);
        var updated  = original.WithScale(5);
        Assert.Equal(5, updated.Scale);
        Assert.True(updated.Smooth);              // unchanged
        Assert.Equal(0.3f, updated.ScanlineIntensity); // unchanged
    }
}
```

- [ ] **Step 2: Run to confirm they fail**

```
dotnet test tests/Machines.Atom.Tests/ /p:CollectCoverage=false --filter "ClassName=Machines.Atom.Tests.DisplayOptionsTests"
```

Expected: compilation failure (file doesn't exist yet — that's fine, create the file first, then expect FAIL or PASS since the code is already there).

> Note: `DisplayOptions` is already implemented, so these tests should go **green immediately**. If any fail, the implementation has a bug that needs fixing.

- [ ] **Step 3: Run to confirm they all pass**

```
dotnet test tests/Machines.Atom.Tests/ /p:CollectCoverage=false --filter "ClassName=Machines.Atom.Tests.DisplayOptionsTests"
```

Expected: all green.

- [ ] **Step 4: Commit**

```
git add tests/Machines.Atom.Tests/DisplayOptionsTests.cs
git commit -m "test: add DisplayOptions unit tests"
```

---

### Task 2: `RaylibHost` smooth and scanline tests

**Files:**
- Modify: `tests/Machines.Atom.Tests/RaylibHostTests.cs`

The existing `FakeRaylibBackend` needs three new capabilities:
1. Track calls to `SetTextureFilter`
2. Allow tests to simulate specific keys being held down
3. Capture the pixel buffer passed to `UpdateTexture`

- [ ] **Step 1: Extend `FakeRaylibBackend` — add tracking fields**

In `RaylibHostTests.cs`, update the `FakeRaylibBackend` private class to add these members (insert after the existing `AudioProcessed` property):

```csharp
// --- new tracking ---
public TextureFilter? LastTextureFilter { get; private set; }
public uint[]? LastTexturePixels { get; private set; }
private readonly Dictionary<KeyboardKey, bool> _keysHeld = new();

public void SetKeyHeld(KeyboardKey key, bool held)
{
    if (held) _keysHeld[key] = true;
    else      _keysHeld.Remove(key);
}
```

Replace the three stub implementations:

```csharp
public void SetTextureFilter(Texture2D texture, TextureFilter filter) =>
    LastTextureFilter = filter;

public void UpdateTexture(Texture2D texture, ReadOnlySpan<uint> pixels)
{
    LastTexturePixels = pixels.ToArray();
}

public bool IsKeyDown(KeyboardKey key) =>
    _keysHeld.TryGetValue(key, out bool held) && held;
```

- [ ] **Step 2: Write new display tests** — add these test methods to `RaylibHostTests`:

```csharp
// ── smooth flag ───────────────────────────────────────────────────────────

[Fact]
public void Constructor_SmoothEnabled_SetsTextureFilterBilinear()
{
    var backend = new FakeRaylibBackend();
    var opts    = new DisplayOptions(Smooth: true);
    using var _ = new RaylibHost(displayOptions: opts, backend: backend);

    Assert.Equal(TextureFilter.Bilinear, backend.LastTextureFilter);
}

[Fact]
public void Constructor_SmoothDisabled_DoesNotSetBilinearFilter()
{
    var backend = new FakeRaylibBackend();
    using var _ = new RaylibHost(displayOptions: new DisplayOptions(Smooth: false), backend: backend);

    // SetTextureFilter should not have been called (or called with Point)
    Assert.True(backend.LastTextureFilter is null or TextureFilter.Point);
}

[Fact]
public void F10Hotkey_TogglesSmoothOn()
{
    var backend = new FakeRaylibBackend();
    var opts    = new DisplayOptions(Smooth: false);
    using var host = new RaylibHost(displayOptions: opts, backend: backend);

    backend.SetKeyHeld(KeyboardKey.F10, true);
    host.PollEvents();

    Assert.Equal(TextureFilter.Bilinear, backend.LastTextureFilter);
}

[Fact]
public void F10Hotkey_TogglesSmoothOff()
{
    var backend = new FakeRaylibBackend();
    var opts    = new DisplayOptions(Smooth: true);
    using var host = new RaylibHost(displayOptions: opts, backend: backend);
    // Filter was set Bilinear on init; now press F10 to toggle off
    backend.SetKeyHeld(KeyboardKey.F10, true);
    host.PollEvents();

    Assert.Equal(TextureFilter.Point, backend.LastTextureFilter);
}

// ── scanlines ─────────────────────────────────────────────────────────────

[Fact]
public void SubmitFrame_WithScanlines_DarkensOddRows()
{
    var backend = new FakeRaylibBackend();
    var opts    = new DisplayOptions(ScanlineIntensity: 0.5f);
    // 2×2 frame: row 0 and row 1
    using var host = new RaylibHost(
        displayOptions: opts,
        frameWidth:  2,
        frameHeight: 2,
        backend: backend);

    // ARGB32: alpha=0xFF, R=200, G=100, B=50 → 0xFF_C8_64_32
    uint inputPixel = 0xFF_C8_64_32u;
    uint[] frame = [inputPixel, inputPixel, inputPixel, inputPixel]; // 4 pixels
    host.SubmitFrame(frame, 2, 2);

    // Row 0 (pixels 0,1): must be unchanged
    // After ARGB→RGBA: r=200, g=100, b=50, a=255 → 0xFF_32_64_C8
    uint expectedRow0 = 200u | (100u << 8) | (50u << 16) | (255u << 24);
    Assert.Equal(expectedRow0, backend.LastTexturePixels![0]);
    Assert.Equal(expectedRow0, backend.LastTexturePixels![1]);

    // Row 1 (pixels 2,3): must be darkened by factor 0.5
    uint expectedRow1 = 100u | (50u << 8) | (25u << 16) | (255u << 24);
    Assert.Equal(expectedRow1, backend.LastTexturePixels![2]);
    Assert.Equal(expectedRow1, backend.LastTexturePixels![3]);
}

[Fact]
public void SubmitFrame_WithNoScanlines_LeavesAllRowsUnchanged()
{
    var backend = new FakeRaylibBackend();
    using var host = new RaylibHost(
        displayOptions: new DisplayOptions(ScanlineIntensity: 0f),
        frameWidth:  2,
        frameHeight: 2,
        backend: backend);

    uint inputPixel = 0xFF_C8_64_32u;
    uint[] frame = [inputPixel, inputPixel, inputPixel, inputPixel];
    host.SubmitFrame(frame, 2, 2);

    // All 4 pixels must be the same (ARGB→RGBA conversion only, no darkening)
    uint expectedPixel = 200u | (100u << 8) | (50u << 16) | (255u << 24);
    Assert.All(backend.LastTexturePixels!, p => Assert.Equal(expectedPixel, p));
}

[Fact]
public void F11Hotkey_CyclesScanlineIntensity_ZeroToPoint3()
{
    var backend = new FakeRaylibBackend();
    var opts    = new DisplayOptions(ScanlineIntensity: 0f);
    using var host = new RaylibHost(displayOptions: opts, backend: backend);

    backend.SetKeyHeld(KeyboardKey.F11, true);
    host.PollEvents();

    Assert.Equal(0.3f, opts.ScanlineIntensity);
}

[Fact]
public void F11Hotkey_CyclesScanlineIntensity_Point3ToPoint5()
{
    var backend = new FakeRaylibBackend();
    var opts    = new DisplayOptions(ScanlineIntensity: 0.3f);
    using var host = new RaylibHost(displayOptions: opts, backend: backend);

    backend.SetKeyHeld(KeyboardKey.F11, true);
    host.PollEvents();

    Assert.Equal(0.5f, opts.ScanlineIntensity);
}

[Fact]
public void F11Hotkey_CyclesScanlineIntensity_Point5ToZero()
{
    var backend = new FakeRaylibBackend();
    var opts    = new DisplayOptions(ScanlineIntensity: 0.5f);
    using var host = new RaylibHost(displayOptions: opts, backend: backend);

    backend.SetKeyHeld(KeyboardKey.F11, true);
    host.PollEvents();

    Assert.Equal(0f, opts.ScanlineIntensity);
}
```

- [ ] **Step 3: Run to confirm they pass**

```
dotnet test tests/Machines.Atom.Tests/ /p:CollectCoverage=false --filter "ClassName=Machines.Atom.Tests.RaylibHostTests"
```

Expected: all green. Fix any failures before continuing.

- [ ] **Step 4: Commit**

```
git add tests/Machines.Atom.Tests/RaylibHostTests.cs
git commit -m "test: add RaylibHost smooth filter and scanline tests"
```

---

### Task 3: `AtomCommandLine` smooth/scanlines parsing tests

**Files:**
- Modify: `tests/Machines.Atom.Tests/AtomCommandLineTests.cs`

- [ ] **Step 1: Add failing tests** — append these methods to `AtomCommandLineTests`:

```csharp
[Fact]
public void Parse_SmoothFlag_SetsSmoothTrue()
{
    var options = AtomCommandLine.Parse(["--basic", "b.rom", "--os", "o.rom", "--smooth"]);
    Assert.True(options.Smooth);
}

[Fact]
public void Parse_NoSmoothFlag_DefaultsFalse()
{
    var options = AtomCommandLine.Parse(["--basic", "b.rom", "--os", "o.rom"]);
    Assert.False(options.Smooth);
}

[Fact]
public void Parse_ScanlinesFlag_SetsScanlineIntensity()
{
    var options = AtomCommandLine.Parse(["--basic", "b.rom", "--os", "o.rom", "--scanlines", "0.5"]);
    Assert.Equal(0.5f, options.ScanlineIntensity);
}

[Fact]
public void Parse_NoScanlinesFlag_DefaultsToZero()
{
    var options = AtomCommandLine.Parse(["--basic", "b.rom", "--os", "o.rom"]);
    Assert.Equal(0f, options.ScanlineIntensity);
}

[Theory]
[InlineData("-0.1")]
[InlineData("1.1")]
[InlineData("abc")]
public void Parse_InvalidScanlines_Throws(string value)
{
    Assert.Throws<ArgumentException>(() =>
        AtomCommandLine.Parse(["--basic", "b.rom", "--os", "o.rom", "--scanlines", value]));
}
```

- [ ] **Step 2: Run to confirm they pass**

```
dotnet test tests/Machines.Atom.Tests/ /p:CollectCoverage=false --filter "ClassName=Machines.Atom.Tests.AtomCommandLineTests"
```

Expected: all green.

- [ ] **Step 3: Commit**

```
git add tests/Machines.Atom.Tests/AtomCommandLineTests.cs
git commit -m "test: add AtomCommandLine smooth and scanlines parsing tests"
```

---

### Task 4: `Vic20CommandLine` smooth/scanlines parsing tests

**Files:**
- Modify: `tests/Machines.Vic20.Tests/Vic20CommandLineTests.cs`

- [ ] **Step 1: Add failing tests** — append these methods to `Vic20CommandLineTests`:

```csharp
[Fact]
public void Parse_SmoothFlag_SetsSmoothTrue()
{
    var options = Vic20CommandLine.Parse(["--basic", "b.rom", "--kernal", "k.rom", "--smooth"]);
    Assert.True(options.Smooth);
}

[Fact]
public void Parse_NoSmoothFlag_DefaultsFalse()
{
    var options = Vic20CommandLine.Parse(["--basic", "b.rom", "--kernal", "k.rom"]);
    Assert.False(options.Smooth);
}

[Fact]
public void Parse_ScanlinesFlag_SetsScanlineIntensity()
{
    var options = Vic20CommandLine.Parse(["--basic", "b.rom", "--kernal", "k.rom", "--scanlines", "0.3"]);
    Assert.Equal(0.3f, options.ScanlineIntensity);
}

[Fact]
public void Parse_NoScanlinesFlag_DefaultsToZero()
{
    var options = Vic20CommandLine.Parse(["--basic", "b.rom", "--kernal", "k.rom"]);
    Assert.Equal(0f, options.ScanlineIntensity);
}

[Theory]
[InlineData("-0.1")]
[InlineData("1.1")]
[InlineData("abc")]
public void Parse_InvalidScanlines_Throws(string value)
{
    Assert.Throws<ArgumentException>(() =>
        Vic20CommandLine.Parse(["--basic", "b.rom", "--kernal", "k.rom", "--scanlines", value]));
}
```

- [ ] **Step 2: Run to confirm they pass**

```
dotnet test tests/Machines.Vic20.Tests/ /p:CollectCoverage=false --filter "ClassName=Machines.Vic20.Tests.Vic20CommandLineTests"
```

Expected: all green.

- [ ] **Step 3: Commit**

```
git add tests/Machines.Vic20.Tests/Vic20CommandLineTests.cs
git commit -m "test: add Vic20CommandLine smooth and scanlines parsing tests"
```

---

### Task 5: Full test run, CLAUDE.md cleanup, and push

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Run the full test suite**

```
dotnet test /p:CollectCoverage=false
```

Expected: all tests pass across all projects. Fix any failures before continuing.

- [ ] **Step 2: Remove the `--smooth` and `--scanlines` items from `CLAUDE.md` Future Work**

In `CLAUDE.md`, remove the two subsections:
- `### Display: scaling filter (--smooth)`
- `### Display: scanlines (--scanlines <intensity>)`

Leave the `### Acorn Electron machine` subsection intact.

Also update the `DisplayOptions` note in the CLAUDE.md VIC-20 section if the wording still refers to it as a future `record` — it's now a `sealed class`.

- [ ] **Step 3: Commit and push**

```
git add CLAUDE.md
git commit -m "docs: mark --smooth and --scanlines as complete; remove from Future Work"
git push
```
