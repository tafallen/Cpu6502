using Raylib_cs;
using System.Numerics;

namespace Adapters.Raylib;

public interface IRaylibBackend
{
    void InitWindow(int width, int height, string title);
    void SetTargetFps(int fps);
    Image GenImageColor(int width, int height, Color color);
    Texture2D LoadTextureFromImage(Image image);
    void UnloadImage(Image image);
    void InitAudioDevice();
    AudioStream LoadAudioStream(uint sampleRate, uint sampleSize, uint channels);
    void PlayAudioStream(AudioStream stream);
    bool WindowShouldClose();
    void PollInputEvents();
    int GetKeyPressed();
    void UpdateTexture(Texture2D texture, ReadOnlySpan<uint> pixels);
    void SetTextureFilter(Texture2D texture, TextureFilter filter);
    void BeginDrawing();
    void ClearBackground(Color color);
    void DrawTextureEx(Texture2D texture, Vector2 position, float rotation, float scale, Color tint);
    void DrawRectangle(int x, int y, int width, int height, Color color);
    void DrawRectangleLines(int x, int y, int width, int height, Color color);
    void DrawText(string text, int x, int y, int fontSize, Color color);
    void EndDrawing();
    bool IsAudioStreamProcessed(AudioStream stream);
    void UpdateAudioStream(AudioStream stream, ReadOnlySpan<short> samples, int count);
    bool IsKeyDown(KeyboardKey key);
    void UnloadTexture(Texture2D texture);
    void UnloadAudioStream(AudioStream stream);
    void CloseAudioDevice();
    void CloseWindow();
}

public sealed class RaylibBackend : IRaylibBackend
{
    public void InitWindow(int width, int height, string title) => Raylib_cs.Raylib.InitWindow(width, height, title);
    public void SetTargetFps(int fps) => Raylib_cs.Raylib.SetTargetFPS(fps);
    public Image GenImageColor(int width, int height, Color color) => Raylib_cs.Raylib.GenImageColor(width, height, color);
    public Texture2D LoadTextureFromImage(Image image) => Raylib_cs.Raylib.LoadTextureFromImage(image);
    public void UnloadImage(Image image) => Raylib_cs.Raylib.UnloadImage(image);
    public void InitAudioDevice() => Raylib_cs.Raylib.InitAudioDevice();
    public AudioStream LoadAudioStream(uint sampleRate, uint sampleSize, uint channels) =>
        Raylib_cs.Raylib.LoadAudioStream(sampleRate, sampleSize, channels);
    public void PlayAudioStream(AudioStream stream) => Raylib_cs.Raylib.PlayAudioStream(stream);
    public bool WindowShouldClose() => Raylib_cs.Raylib.WindowShouldClose();
    public void PollInputEvents() => Raylib_cs.Raylib.PollInputEvents();
    public int GetKeyPressed() => (int)Raylib_cs.Raylib.GetKeyPressed();

    public unsafe void UpdateTexture(Texture2D texture, ReadOnlySpan<uint> pixels)
    {
        fixed (uint* ptr = pixels)
            Raylib_cs.Raylib.UpdateTexture(texture, ptr);
    }

    public void SetTextureFilter(Texture2D texture, TextureFilter filter) =>
        Raylib_cs.Raylib.SetTextureFilter(texture, filter);


    public void BeginDrawing() => Raylib_cs.Raylib.BeginDrawing();
    public void ClearBackground(Color color) => Raylib_cs.Raylib.ClearBackground(color);
    public void DrawTextureEx(Texture2D texture, Vector2 position, float rotation, float scale, Color tint) =>
        Raylib_cs.Raylib.DrawTextureEx(texture, position, rotation, scale, tint);
    public void DrawRectangle(int x, int y, int width, int height, Color color) =>
        Raylib_cs.Raylib.DrawRectangle(x, y, width, height, color);
    public void DrawRectangleLines(int x, int y, int width, int height, Color color) =>
        Raylib_cs.Raylib.DrawRectangleLines(x, y, width, height, color);
    public void DrawText(string text, int x, int y, int fontSize, Color color) =>
        Raylib_cs.Raylib.DrawText(text, x, y, fontSize, color);
    public void EndDrawing() => Raylib_cs.Raylib.EndDrawing();
    public bool IsAudioStreamProcessed(AudioStream stream) => Raylib_cs.Raylib.IsAudioStreamProcessed(stream);

    public unsafe void UpdateAudioStream(AudioStream stream, ReadOnlySpan<short> samples, int count)
    {
        fixed (short* ptr = samples)
            Raylib_cs.Raylib.UpdateAudioStream(stream, ptr, count);
    }

    public bool IsKeyDown(KeyboardKey key) => Raylib_cs.Raylib.IsKeyDown(key);
    public void UnloadTexture(Texture2D texture) => Raylib_cs.Raylib.UnloadTexture(texture);
    public void UnloadAudioStream(AudioStream stream) => Raylib_cs.Raylib.UnloadAudioStream(stream);
    public void CloseAudioDevice() => Raylib_cs.Raylib.CloseAudioDevice();
    public void CloseWindow() => Raylib_cs.Raylib.CloseWindow();
}

