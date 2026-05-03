using Machines.Common;
using Raylib_cs;
using System.Numerics;

namespace Adapters.Raylib;

/// <summary>
/// Cross-platform host window using Raylib.
/// Implements both IVideoSink (renders pixel frames) and IPhysicalKeyboard (queries key state).
/// The emulator loop drives both through the same object — no separate keyboard plumbing needed.
///
/// Typical loop:
/// <code>
///   using var host = new RaylibHost("Acorn Atom", scale: 3);
///   while (host.IsRunning)
///   {
///       host.PollEvents();
///       for (int i = 0; i &lt; CyclesPerFrame; i++) machine.Step();
///       machine.RenderFrame(host);   // host is the IVideoSink
///   }
/// </code>
/// </summary>
public sealed class RaylibHost : IVideoSink, IPhysicalKeyboard, IAudioSink, IDisposable
{
    private const int DefaultFrameRate = 50;
    private const int DefaultAudioSampleRate = 44100;

    private readonly int _scale;
    private readonly int _frameWidth;
    private readonly int _frameHeight;
    private readonly bool _logKeypresses;
    private readonly IRaylibBackend _backend;
    private Texture2D   _texture;
    private AudioStream _audioStream;
    private readonly uint[]  _rgbaBuffer;
    private short[] _audioBuffer;
    private bool _disposed;

    public RaylibHost(
        string title = "Acorn Atom",
        int scale = 3,
        int frameWidth = 256,
        int frameHeight = 192,
        bool logKeypresses = false,
        int targetFps = DefaultFrameRate,
        int audioSampleRate = DefaultAudioSampleRate,
        IRaylibBackend? backend = null)
    {
        _scale       = scale;
        _frameWidth  = frameWidth;
        _frameHeight = frameHeight;
        _logKeypresses = logKeypresses;
        _backend = backend ?? new RaylibBackend();
        _backend.InitWindow(frameWidth * scale, frameHeight * scale, title);
        _backend.SetTargetFps(targetFps);

        // Video texture
        var img = _backend.GenImageColor(frameWidth, frameHeight, Color.Black);
        _texture = _backend.LoadTextureFromImage(img);
        _backend.UnloadImage(img);

        // Audio: mono, 16-bit PCM
        _backend.InitAudioDevice();
        _audioStream = _backend.LoadAudioStream((uint)audioSampleRate, 16, 1);
        _backend.PlayAudioStream(_audioStream);

        _rgbaBuffer  = new uint[frameWidth * frameHeight];
        _audioBuffer = [];
    }

    public bool IsRunning => !_backend.WindowShouldClose();

    /// <summary>Process OS events and update key state. Call once per emulator iteration.</summary>
    public void PollEvents()
    {
        _backend.PollInputEvents();
        if (!_logKeypresses) return;

        // Optional verbose key logging for input debugging.
        int kp;
        while ((kp = _backend.GetKeyPressed()) != 0)
            Console.WriteLine($"[key] Raylib saw keypress: {(KeyboardKey)kp} ({kp})");
    }

    // ── IVideoSink ────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts the ARGB32 pixel buffer to RGBA32 (Raylib's native format),
    /// uploads it to the GPU texture, and draws it scaled to the window.
    /// </summary>
    public void SubmitFrame(ReadOnlySpan<uint> pixels, int width, int height)
    {
        // Convert ARGB32 (0xAARRGGBB) → RGBA32 (memory: R G B A = 0xAABBGGRR as uint)
        int count = Math.Min(pixels.Length, _rgbaBuffer.Length);
        for (int i = 0; i < count; i++)
        {
            uint argb = pixels[i];
            uint r = (argb >> 16) & 0xFF;
            uint g = (argb >>  8) & 0xFF;
            uint b =  argb        & 0xFF;
            uint a = (argb >> 24) & 0xFF;
            _rgbaBuffer[i] = r | (g << 8) | (b << 16) | (a << 24);
        }

        _backend.UpdateTexture(_texture, _rgbaBuffer);

        _backend.BeginDrawing();
        _backend.ClearBackground(Color.Black);
        _backend.DrawTextureEx(
            _texture,
            Vector2.Zero,
            rotation: 0f,
            scale: _scale,
            Color.White);
        _backend.EndDrawing();
    }

    // ── IAudioSink ────────────────────────────────────────────────────────────

    /// <summary>
    /// Feeds the sample buffer into Raylib's audio stream.
    /// If the stream is still playing the previous buffer, the frame is silently dropped
    /// (preferable to stalling the emulator loop).
    /// </summary>
    public void SubmitSamples(ReadOnlySpan<short> samples, int sampleRate)
    {
        if (!_backend.IsAudioStreamProcessed(_audioStream)) return;

        int count = samples.Length;
        EnsureAudioCapacity(count);
        samples[..count].CopyTo(_audioBuffer);
        _backend.UpdateAudioStream(_audioStream, _audioBuffer, count);
    }

    private void EnsureAudioCapacity(int sampleCount)
    {
        if (sampleCount <= _audioBuffer.Length) return;
        Array.Resize(ref _audioBuffer, sampleCount);
    }

    // ── IPhysicalKeyboard ─────────────────────────────────────────────────────

    public bool IsKeyDown(PhysicalKey key) =>
        RaylibKeyMap.TryGet(key, out var rk) && _backend.IsKeyDown(rk);

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _backend.UnloadTexture(_texture);
        _backend.UnloadAudioStream(_audioStream);
        _backend.CloseAudioDevice();
        _backend.CloseWindow();
    }
}
