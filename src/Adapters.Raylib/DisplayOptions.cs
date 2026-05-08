namespace Adapters.Raylib;

/// <summary>
/// Display configuration options for rendering video frames.
/// Allows control over scaling, filtering, and CRT effects.
/// </summary>
public sealed record DisplayOptions(
    int Scale = 3,
    bool Smooth = false,
    float ScanlineIntensity = 0f)
{
    /// <summary>
    /// Validates that ScanlineIntensity is in the range [0.0, 1.0].
    /// </summary>
    public bool IsValid => ScanlineIntensity >= 0f && ScanlineIntensity <= 1f;

    /// <summary>
    /// Returns a new DisplayOptions with scale adjusted.
    /// </summary>
    public DisplayOptions WithScale(int newScale) => this with { Scale = newScale };

    /// <summary>
    /// Returns a new DisplayOptions with smooth filtering enabled/disabled.
    /// </summary>
    public DisplayOptions WithSmooth(bool smooth) => this with { Smooth = smooth };

    /// <summary>
    /// Returns a new DisplayOptions with scanline intensity adjusted.
    /// </summary>
    public DisplayOptions WithScanlines(float intensity) =>
        intensity >= 0f && intensity <= 1f
            ? this with { ScanlineIntensity = intensity }
            : throw new ArgumentOutOfRangeException(nameof(intensity), "Must be between 0.0 and 1.0");
}
