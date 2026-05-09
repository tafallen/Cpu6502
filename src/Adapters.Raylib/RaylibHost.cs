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
///   using var host = new RaylibHost("Acorn Atom", new DisplayOptions(scale: 3));
///   while (host.IsRunning)
///   {
///       host.PollEvents();
///       for (int i = 0; i &lt; CyclesPerFrame; i++) machine.Step();
///       machine.RenderFrame(host);   // host is the IVideoSink
///   }
/// </code>
/// 
/// Runtime hotkey toggles:
/// - F10: Toggle bilinear texture filtering
/// - F11: Cycle scanline intensity (0 → 0.3 → 0.5 → 0)
/// </summary>
public sealed class RaylibHost : IVideoSink, IPhysicalKeyboard, IAudioSink, IDisposable
{
    private const int DefaultFrameRate = 50;
    private const int DefaultAudioSampleRate = 44100;
    private const int OverlayDurationFrames = 60;  // Show overlay for 60 frames (~1.2 seconds at 50 FPS)

    private readonly DisplayOptions _displayOptions;
    private readonly int _frameWidth;
    private readonly int _frameHeight;
    private readonly bool _logKeypresses;
    private readonly IRaylibBackend _backend;
    private Texture2D   _texture;
    private AudioStream _audioStream;
    private readonly uint[]  _rgbaBuffer;
    private short[] _audioBuffer;
    private bool _disposed;

    // Overlay state for hotkey feedback
    private string? _overlayMessage;
    private int _overlayFrameCounter;

    /// <summary>
    /// Create a new RaylibHost with display options.
    /// </summary>
    public RaylibHost(
        string title = "Acorn Atom",
        DisplayOptions? displayOptions = null,
        int frameWidth = 256,
        int frameHeight = 192,
        bool logKeypresses = false,
        int targetFps = DefaultFrameRate,
        int audioSampleRate = DefaultAudioSampleRate,
        IRaylibBackend? backend = null)
    {
        _displayOptions = displayOptions ?? new DisplayOptions();
        _frameWidth  = frameWidth;
        _frameHeight = frameHeight;
        _logKeypresses = logKeypresses;
        _backend = backend ?? new RaylibBackend();
        _backend.InitWindow(frameWidth * _displayOptions.Scale, frameHeight * _displayOptions.Scale, title);
        _backend.SetTargetFps(targetFps);

        // Video texture
        var img = _backend.GenImageColor(frameWidth, frameHeight, Color.Black);
        _texture = _backend.LoadTextureFromImage(img);
        _backend.UnloadImage(img);

        // Apply texture filtering if smooth is enabled
        if (_displayOptions.Smooth)
            _backend.SetTextureFilter(_texture, TextureFilter.Bilinear);

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
        HandleDisplayHotkeys();

        if (!_logKeypresses) return;

        // Optional verbose key logging for input debugging.
        int kp;
        while ((kp = _backend.GetKeyPressed()) != 0)
            Console.WriteLine($"[key] Raylib saw keypress: {(KeyboardKey)kp} ({kp})");
    }

    /// <summary>
    /// Handle runtime hotkey toggles for display options.
    /// F10: Toggle smooth filtering
    /// F11: Cycle scanline intensity (0 → 0.3 → 0.5 → 0)
    /// </summary>
    private void HandleDisplayHotkeys()
    {
        if (_backend.IsKeyDown(KeyboardKey.F10))
        {
            _displayOptions.Smooth = !_displayOptions.Smooth;
            ApplyTextureFilter(_displayOptions.Smooth);
            SetOverlayMessage($"Smooth: {(_displayOptions.Smooth ? "ON" : "OFF")}");
        }

        if (_backend.IsKeyDown(KeyboardKey.F11))
        {
            // Cycle: 0 → 0.3 → 0.5 → 0
            _displayOptions.ScanlineIntensity = _displayOptions.ScanlineIntensity switch
            {
                0f => 0.3f,
                0.3f => 0.5f,
                _ => 0f
            };
            SetOverlayMessage($"Scanlines: {_displayOptions.ScanlineIntensity:F1}");
        }
    }

    /// <summary>
    /// Apply texture filter at runtime (rebind to texture).
    /// </summary>
    private void ApplyTextureFilter(bool smooth)
    {
        _backend.SetTextureFilter(_texture, smooth ? TextureFilter.Bilinear : TextureFilter.Point);
    }

    /// <summary>
    /// Set overlay message to display for next N frames.
    /// </summary>
    private void SetOverlayMessage(string message)
    {
        _overlayMessage = message;
        _overlayFrameCounter = 0;
    }

    // ── IVideoSink ────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts the ARGB32 pixel buffer to RGBA32 (Raylib's native format),
    /// applies scanlines if configured, uploads it to the GPU texture, and draws it scaled to the window.
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

        // Apply scanlines if configured
        if (_displayOptions.ScanlineIntensity > 0f)
            ApplyScanlines();

        _backend.UpdateTexture(_texture, _rgbaBuffer);

        _backend.BeginDrawing();
        _backend.ClearBackground(Color.Black);
        _backend.DrawTextureEx(
            _texture,
            Vector2.Zero,
            rotation: 0f,
            scale: _displayOptions.Scale,
            Color.White);
        
        // Render overlay message if active
        RenderOverlay();
        
        _backend.EndDrawing();
    }

    /// <summary>
    /// Render overlay message (hotkey feedback) for a fixed duration.
    /// Message fades after OverlayDurationFrames.
    /// </summary>
    private void RenderOverlay()
    {
        if (_overlayMessage == null)
            return;

        if (_overlayFrameCounter >= OverlayDurationFrames)
        {
            _overlayMessage = null;
            return;
        }

        // Calculate fade: full opacity for first 75%, then fade to 0
        float fadeStartFrame = OverlayDurationFrames * 0.75f;
        float alpha = _overlayFrameCounter < fadeStartFrame
            ? 1f
            : 1f - (_overlayFrameCounter - fadeStartFrame) / (OverlayDurationFrames - fadeStartFrame);

        // Draw semi-transparent background box
        int boxWidth = 200;
        int boxHeight = 40;
        int boxX = (_frameWidth * _displayOptions.Scale - boxWidth) / 2;
        int boxY = 20;

        Color bgColor = new((byte)0, (byte)0, (byte)0, (byte)(200 * alpha));
        _backend.DrawRectangle(boxX, boxY, boxWidth, boxHeight, bgColor);

        // Draw border
        Color borderColor = new((byte)255, (byte)255, (byte)255, (byte)(255 * alpha));
        _backend.DrawRectangleLines(boxX, boxY, boxWidth, boxHeight, borderColor);

        // Draw text
        Color textColor = new((byte)255, (byte)255, (byte)255, (byte)(255 * alpha));
        int textX = boxX + 10;
        int textY = boxY + 10;
        _backend.DrawText(_overlayMessage, textX, textY, 20, textColor);

        _overlayFrameCounter++;
    }

    /// <summary>
    /// Darkens alternating horizontal rows to simulate CRT scanlines.
    /// Intensity controls the darkness: 0 = no effect, 1 = full black scanlines.
    /// </summary>
    private void ApplyScanlines()
    {
        float darknessFactor = 1f - _displayOptions.ScanlineIntensity;

        for (int y = 1; y < _frameHeight; y += 2)
        {
            int rowStart = y * _frameWidth;
            int rowEnd = rowStart + _frameWidth;

            for (int i = rowStart; i < rowEnd; i++)
            {
                uint rgba = _rgbaBuffer[i];
                byte r = (byte)((rgba & 0xFF) * darknessFactor);
                byte g = (byte)(((rgba >> 8) & 0xFF) * darknessFactor);
                byte b = (byte)(((rgba >> 16) & 0xFF) * darknessFactor);
                byte a = (byte)((rgba >> 24) & 0xFF);
                _rgbaBuffer[i] = r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
            }
        }
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
