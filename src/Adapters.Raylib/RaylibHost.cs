using Machines.Atom;
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
    private readonly int _scale;
    private Texture2D   _texture;
    private AudioStream _audioStream;
    private readonly uint[]  _rgbaBuffer;
    private readonly short[] _audioBuffer;
    private bool _disposed;

    public RaylibHost(string title = "Acorn Atom", int scale = 3)
    {
        _scale = scale;
        Raylib_cs.Raylib.InitWindow(256 * scale, 192 * scale, title);
        Raylib_cs.Raylib.SetTargetFPS(50);

        // Video texture
        var img = Raylib_cs.Raylib.GenImageColor(256, 192, Color.Black);
        _texture = Raylib_cs.Raylib.LoadTextureFromImage(img);
        Raylib_cs.Raylib.UnloadImage(img);

        // Audio: mono, 16-bit, 44100 Hz — matches AtomSoundAdapter
        Raylib_cs.Raylib.InitAudioDevice();
        _audioStream = Raylib_cs.Raylib.LoadAudioStream(44100, 16, 1);
        Raylib_cs.Raylib.PlayAudioStream(_audioStream);

        _rgbaBuffer  = new uint[256 * 192];
        _audioBuffer = new short[AtomSoundAdapter.SamplesPerFrame];
    }

    public bool IsRunning => !Raylib_cs.Raylib.WindowShouldClose();

    /// <summary>Process OS events and update key state. Call once per emulator iteration.</summary>
    public void PollEvents()
    {
        Raylib_cs.Raylib.PollInputEvents();
        // Log any key pressed this frame so we can verify Raylib is receiving input
        int kp;
        while ((kp = (int)Raylib_cs.Raylib.GetKeyPressed()) != 0)
            Console.WriteLine($"[key] Raylib saw keypress: {(KeyboardKey)kp} ({kp})");
    }

    // ── IVideoSink ────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts the ARGB32 pixel buffer to RGBA32 (Raylib's native format),
    /// uploads it to the GPU texture, and draws it scaled to the window.
    /// </summary>
    public unsafe void SubmitFrame(ReadOnlySpan<uint> pixels, int width, int height)
    {
        // Convert ARGB32 (0xAARRGGBB) → RGBA32 (memory: R G B A = 0xAABBGGRR as uint)
        for (int i = 0; i < pixels.Length; i++)
        {
            uint argb = pixels[i];
            uint r = (argb >> 16) & 0xFF;
            uint g = (argb >>  8) & 0xFF;
            uint b =  argb        & 0xFF;
            uint a = (argb >> 24) & 0xFF;
            _rgbaBuffer[i] = r | (g << 8) | (b << 16) | (a << 24);
        }

        fixed (uint* ptr = _rgbaBuffer)
            Raylib_cs.Raylib.UpdateTexture(_texture, ptr);

        Raylib_cs.Raylib.BeginDrawing();
        Raylib_cs.Raylib.ClearBackground(Color.Black);
        Raylib_cs.Raylib.DrawTextureEx(
            _texture,
            Vector2.Zero,
            rotation: 0f,
            scale: _scale,
            Color.White);
        Raylib_cs.Raylib.EndDrawing();
    }

    // ── IAudioSink ────────────────────────────────────────────────────────────

    /// <summary>
    /// Feeds the sample buffer into Raylib's audio stream.
    /// If the stream is still playing the previous buffer, the frame is silently dropped
    /// (preferable to stalling the emulator loop).
    /// </summary>
    public unsafe void SubmitSamples(ReadOnlySpan<short> samples, int sampleRate)
    {
        if (!Raylib_cs.Raylib.IsAudioStreamProcessed(_audioStream)) return;
        int count = Math.Min(samples.Length, _audioBuffer.Length);
        samples[..count].CopyTo(_audioBuffer);
        fixed (short* ptr = _audioBuffer)
            Raylib_cs.Raylib.UpdateAudioStream(_audioStream, ptr, count);
    }

    // ── IPhysicalKeyboard ─────────────────────────────────────────────────────

    public bool IsKeyDown(PhysicalKey key) =>
        RaylibKeyMap.TryGet(key, out var rk) && Raylib_cs.Raylib.IsKeyDown(rk);

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Raylib_cs.Raylib.UnloadTexture(_texture);
        Raylib_cs.Raylib.UnloadAudioStream(_audioStream);
        Raylib_cs.Raylib.CloseAudioDevice();
        Raylib_cs.Raylib.CloseWindow();
    }
}
