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
    public void IsValid_ReturnsFalseWhenScanlineIntensityDirectlyMutated()
    {
        var opts = new DisplayOptions();
        opts.ScanlineIntensity = 1.5f;  // bypass validation via direct property set
        Assert.False(opts.IsValid);
    }

    [Fact]
    public void WithSmooth_ReturnsNewInstanceWithSmoothSet()
    {
        var original = new DisplayOptions(Scale: 2, Smooth: false, ScanlineIntensity: 0.3f);
        var updated  = original.WithSmooth(true);
        Assert.True(updated.Smooth);
        Assert.Equal(2, updated.Scale);
        Assert.Equal(0.3f, updated.ScanlineIntensity);
        Assert.False(original.Smooth);
    }

    [Fact]
    public void WithScanlines_ReturnsNewInstanceWithIntensitySet()
    {
        var original = new DisplayOptions(Scale: 2, Smooth: true, ScanlineIntensity: 0f);
        var updated  = original.WithScanlines(0.5f);
        Assert.Equal(0.5f, updated.ScanlineIntensity);
        Assert.Equal(2, updated.Scale);
        Assert.True(updated.Smooth);
        Assert.Equal(0f, original.ScanlineIntensity);
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
        Assert.True(updated.Smooth);
        Assert.Equal(0.3f, updated.ScanlineIntensity);
        Assert.Equal(3, original.Scale);          // original unmodified
    }
}
