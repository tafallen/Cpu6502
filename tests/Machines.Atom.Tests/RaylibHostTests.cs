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

    private sealed class FakeRaylibBackend : IRaylibBackend
    {
        private readonly Queue<int> _keypresses = new();

        public bool AudioProcessed { get; set; }
        public int LastAudioCount { get; private set; }
        public short[]? LastAudioSamples { get; private set; }

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
        public void UpdateTexture(Texture2D texture, ReadOnlySpan<uint> pixels) { }
        public void BeginDrawing() { }
        public void ClearBackground(Color color) { }
        public void DrawTextureEx(Texture2D texture, Vector2 position, float rotation, float scale, Color tint) { }
        public void EndDrawing() { }
        public bool IsAudioStreamProcessed(AudioStream stream) => AudioProcessed;

        public void UpdateAudioStream(AudioStream stream, ReadOnlySpan<short> samples, int count)
        {
            LastAudioCount = count;
            LastAudioSamples = samples[..count].ToArray();
        }

        public bool IsKeyDown(KeyboardKey key) => false;
        public void UnloadTexture(Texture2D texture) { }
        public void UnloadAudioStream(AudioStream stream) { }
        public void CloseAudioDevice() { }
        public void CloseWindow() { }
    }
}

