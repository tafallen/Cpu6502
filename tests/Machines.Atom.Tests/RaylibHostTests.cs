using Adapters.Raylib;
using Raylib_cs;
using System.Numerics;

namespace Machines.Atom.Tests;

public class RaylibHostTests
{
    [Fact]
    public void SubmitSamples_ResizesBufferAndForwardsAllSamples()
    {
        var backend = new FakeRaylibBackend { AudioProcessed = true };
        using var host = new RaylibHost(backend: backend);
        short[] samples = Enumerable.Range(0, 1200).Select(i => (short)i).ToArray();

        host.SubmitSamples(samples, 44100);

        Assert.Equal(1200, backend.LastAudioCount);
        Assert.NotNull(backend.LastAudioSamples);
        Assert.Equal(1200, backend.LastAudioSamples!.Length);
        Assert.Equal((short)1199, backend.LastAudioSamples[1199]);
    }

    [Fact]
    public void PollEvents_DoesNotLogByDefault()
    {
        var backend = new FakeRaylibBackend();
        backend.EnqueueKeypress((int)KeyboardKey.A);
        backend.EnqueueKeypress(0);

        using var host = new RaylibHost(backend: backend);
        using var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try
        {
            host.PollEvents();
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal(string.Empty, sw.ToString());
    }

    [Fact]
    public void PollEvents_LogsWhenEnabled()
    {
        var backend = new FakeRaylibBackend();
        backend.EnqueueKeypress((int)KeyboardKey.A);
        backend.EnqueueKeypress(0);

        using var host = new RaylibHost(logKeypresses: true, backend: backend);
        using var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try
        {
            host.PollEvents();
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("[key] Raylib saw keypress:", sw.ToString());
    }

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

        Assert.Null(backend.LastTextureFilter);
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
        using var host = new RaylibHost(
            displayOptions: opts,
            frameWidth:  2,
            frameHeight: 2,
            backend: backend);

        backend.SetKeyHeld(KeyboardKey.F11, true);
        host.PollEvents();

        // After cycling 0→0.3, odd rows should be darkened by factor 0.7
        // Submit a 2×2 frame: ARGB 0xFF_64_64_64 (all channels = 100)
        uint inputPixel = 0xFF_64_64_64u;
        host.SubmitFrame([inputPixel, inputPixel, inputPixel, inputPixel], 2, 2);

        // Row 0 (even): unchanged → R=100, G=100, B=100, A=255
        uint expectedEven = 100u | (100u << 8) | (100u << 16) | (255u << 24);
        Assert.Equal(expectedEven, backend.LastTexturePixels![0]);

        // Row 1 (odd): darkened by 0.7 → floor(100*0.7)=70
        uint expectedOdd = 70u | (70u << 8) | (70u << 16) | (255u << 24);
        Assert.Equal(expectedOdd, backend.LastTexturePixels![2]);
    }

    [Fact]
    public void F11Hotkey_CyclesScanlineIntensity_Point3ToPoint5()
    {
        var backend = new FakeRaylibBackend();
        var opts    = new DisplayOptions(ScanlineIntensity: 0.3f);
        using var host = new RaylibHost(
            displayOptions: opts,
            frameWidth:  2,
            frameHeight: 2,
            backend: backend);

        backend.SetKeyHeld(KeyboardKey.F11, true);
        host.PollEvents();

        // After cycling 0.3→0.5, odd rows should be darkened by factor 0.5
        uint inputPixel = 0xFF_64_64_64u;
        host.SubmitFrame([inputPixel, inputPixel, inputPixel, inputPixel], 2, 2);

        uint expectedEven = 100u | (100u << 8) | (100u << 16) | (255u << 24);
        Assert.Equal(expectedEven, backend.LastTexturePixels![0]);

        // Row 1 (odd): darkened by 0.5 → 50
        uint expectedOdd = 50u | (50u << 8) | (50u << 16) | (255u << 24);
        Assert.Equal(expectedOdd, backend.LastTexturePixels![2]);
    }

    [Fact]
    public void F11Hotkey_CyclesScanlineIntensity_Point5ToZero()
    {
        var backend = new FakeRaylibBackend();
        var opts    = new DisplayOptions(ScanlineIntensity: 0.5f);
        using var host = new RaylibHost(
            displayOptions: opts,
            frameWidth:  2,
            frameHeight: 2,
            backend: backend);

        backend.SetKeyHeld(KeyboardKey.F11, true);
        host.PollEvents();

        // After cycling 0.5→0, no darkening should occur
        uint inputPixel = 0xFF_64_64_64u;
        host.SubmitFrame([inputPixel, inputPixel, inputPixel, inputPixel], 2, 2);

        uint expectedPixel = 100u | (100u << 8) | (100u << 16) | (255u << 24);
        // All rows unchanged
        Assert.All(backend.LastTexturePixels!, p => Assert.Equal(expectedPixel, p));
    }

    private sealed class FakeRaylibBackend : IRaylibBackend
    {
        private readonly Queue<int> _keypresses = new();

        public bool AudioProcessed { get; set; }
        public int LastAudioCount { get; private set; }
        public short[]? LastAudioSamples { get; private set; }

        // --- new tracking ---
        public TextureFilter? LastTextureFilter { get; private set; }
        public uint[]? LastTexturePixels { get; private set; }
        private readonly Dictionary<KeyboardKey, bool> _keysHeld = new();

        public void SetKeyHeld(KeyboardKey key, bool held)
        {
            if (held) _keysHeld[key] = true;
            else      _keysHeld.Remove(key);
        }

        public void EnqueueKeypress(int key) => _keypresses.Enqueue(key);

        public void InitWindow(int width, int height, string title) { }
        public void SetTargetFps(int fps) { }
        public Image GenImageColor(int width, int height, Color color) => new();
        public Texture2D LoadTextureFromImage(Image image) => new();
        public void UnloadImage(Image image) { }
        public void InitAudioDevice() { }
        public AudioStream LoadAudioStream(uint sampleRate, uint sampleSize, uint channels) => new();
        public void PlayAudioStream(AudioStream stream) { }
        public bool WindowShouldClose() => false;
        public void PollInputEvents() { }
        public int GetKeyPressed() => _keypresses.Count > 0 ? _keypresses.Dequeue() : 0;

        public void UpdateTexture(Texture2D texture, ReadOnlySpan<uint> pixels)
        {
            LastTexturePixels = pixels.ToArray();
        }

        public void SetTextureFilter(Texture2D texture, TextureFilter filter) =>
            LastTextureFilter = filter;

        public void BeginDrawing() { }
        public void ClearBackground(Color color) { }
        public void DrawTextureEx(Texture2D texture, Vector2 position, float rotation, float scale, Color tint) { }
        public void DrawRectangle(int x, int y, int width, int height, Color color) { }
        public void DrawRectangleLines(int x, int y, int width, int height, Color color) { }
        public void DrawText(string text, int x, int y, int fontSize, Color color) { }
        public void EndDrawing() { }
        public bool IsAudioStreamProcessed(AudioStream stream) => AudioProcessed;

        public void UpdateAudioStream(AudioStream stream, ReadOnlySpan<short> samples, int count)
        {
            LastAudioCount = count;
            LastAudioSamples = samples[..count].ToArray();
        }

        public bool IsKeyDown(KeyboardKey key) =>
            _keysHeld.TryGetValue(key, out bool held) && held;

        public void UnloadTexture(Texture2D texture) { }
        public void UnloadAudioStream(AudioStream stream) { }
        public void CloseAudioDevice() { }
        public void CloseWindow() { }
    }
}

