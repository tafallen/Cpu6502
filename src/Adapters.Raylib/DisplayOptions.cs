namespace Adapters.Raylib;

/// <summary>
/// Display configuration options for rendering video frames.
/// Allows control over scaling, filtering, and CRT effects.
/// Properties are mutable to support runtime hotkey toggles.
/// </summary>
public sealed class DisplayOptions
{
    public int Scale { get; set; }
    public bool Smooth { get; set; }
    public float ScanlineIntensity { get; set; }

    /// <summary>
    /// Create display options with specified values.
    /// </summary>
    public DisplayOptions(int Scale = 3, bool Smooth = false, float ScanlineIntensity = 0f)
    {
        this.Scale = Scale;
        this.Smooth = Smooth;
        this.ScanlineIntensity = ValidateScanlineIntensity(ScanlineIntensity);
    }

    /// <summary>
    /// Validates that ScanlineIntensity is in the range [0.0, 1.0].
    /// </summary>
    public bool IsValid => ScanlineIntensity >= 0f && ScanlineIntensity <= 1f;

    /// <summary>
    /// Set scanline intensity with validation.
    /// </summary>
    public void SetScanlineIntensity(float intensity)
    {
        ScanlineIntensity = ValidateScanlineIntensity(intensity);
    }

    private static float ValidateScanlineIntensity(float intensity)
    {
        if (intensity < 0f || intensity > 1f)
            throw new ArgumentOutOfRangeException(nameof(intensity), "Must be between 0.0 and 1.0");
        return intensity;
    }

    /// <summary>
    /// Returns a new DisplayOptions with scale adjusted (for backward compatibility with immutable pattern).
    /// </summary>
    public DisplayOptions WithScale(int newScale) => new(newScale, Smooth, ScanlineIntensity);

    /// <summary>
    /// Returns a new DisplayOptions with smooth filtering enabled/disabled (for backward compatibility).
    /// </summary>
    public DisplayOptions WithSmooth(bool smooth) => new(Scale, smooth, ScanlineIntensity);

    /// <summary>
    /// Returns a new DisplayOptions with scanline intensity adjusted (for backward compatibility).
    /// </summary>
    public DisplayOptions WithScanlines(float intensity) => new(Scale, Smooth, ValidateScanlineIntensity(intensity));
}
