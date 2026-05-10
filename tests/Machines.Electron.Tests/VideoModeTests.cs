using Machines.Electron;
using Xunit;

namespace Machines.Electron.Tests;

/// <summary>
/// Video mode tests — verify all 7 modes have correct properties (resolution, color depth, RAM base).
/// </summary>
public class VideoModeTests
{
    [Fact]
    public void Mode0_Properties()
    {
        var props = VideoModeProperties.GetMode(VideoMode.Mode0);
        Assert.Equal(640, props.Width);
        Assert.Equal(256, props.Height);
        Assert.Equal(1, props.BitsPerPixel);  // 2-colour
        Assert.Equal(0x3000, props.RamBase);
    }

    [Fact]
    public void Mode1_Properties()
    {
        var props = VideoModeProperties.GetMode(VideoMode.Mode1);
        Assert.Equal(320, props.Width);
        Assert.Equal(256, props.Height);
        Assert.Equal(2, props.BitsPerPixel);  // 4-colour
        Assert.Equal(0x3000, props.RamBase);
    }

    [Fact]
    public void Mode2_Properties()
    {
        var props = VideoModeProperties.GetMode(VideoMode.Mode2);
        Assert.Equal(160, props.Width);
        Assert.Equal(256, props.Height);
        Assert.Equal(4, props.BitsPerPixel);  // 16-colour
        Assert.Equal(0x3000, props.RamBase);
    }

    [Fact]
    public void Mode3_Properties()
    {
        var props = VideoModeProperties.GetMode(VideoMode.Mode3);
        Assert.Equal(640, props.Width);
        Assert.Equal(200, props.Height);
        Assert.Equal(1, props.BitsPerPixel);  // 2-colour text
        Assert.Equal(0x4000, props.RamBase);
    }

    [Fact]
    public void Mode4_Properties()
    {
        var props = VideoModeProperties.GetMode(VideoMode.Mode4);
        Assert.Equal(320, props.Width);
        Assert.Equal(256, props.Height);
        Assert.Equal(1, props.BitsPerPixel);  // 2-colour
        Assert.Equal(0x5800, props.RamBase);
    }

    [Fact]
    public void Mode5_Properties()
    {
        var props = VideoModeProperties.GetMode(VideoMode.Mode5);
        Assert.Equal(160, props.Width);
        Assert.Equal(256, props.Height);
        Assert.Equal(2, props.BitsPerPixel);  // 4-colour
        Assert.Equal(0x5800, props.RamBase);
    }

    [Fact]
    public void Mode6_Properties()
    {
        var props = VideoModeProperties.GetMode(VideoMode.Mode6);
        Assert.Equal(320, props.Width);
        Assert.Equal(200, props.Height);
        Assert.Equal(1, props.BitsPerPixel);  // 2-colour text
        Assert.Equal(0x6000, props.RamBase);
    }

    [Fact]
    public void VideoMode_DefaultsToMode0()
    {
        var ula = new ElectronUla();
        Assert.Equal(VideoMode.Mode0, ula.VideoMode);
    }

    [Fact]
    public void VideoMode_CanBeChanged()
    {
        var ula = new ElectronUla();
        
        ula.VideoMode = VideoMode.Mode2;
        Assert.Equal(VideoMode.Mode2, ula.VideoMode);

        ula.VideoMode = VideoMode.Mode6;
        Assert.Equal(VideoMode.Mode6, ula.VideoMode);
    }

    [Fact]
    public void CurrentVideoModeProperties_ReturnsCorrectMode()
    {
        var ula = new ElectronUla();
        
        ula.VideoMode = VideoMode.Mode0;
        var props = ula.CurrentVideoModeProperties;
        Assert.Equal(640, props.Width);
        Assert.Equal(0x3000, props.RamBase);

        ula.VideoMode = VideoMode.Mode3;
        props = ula.CurrentVideoModeProperties;
        Assert.Equal(640, props.Width);
        Assert.Equal(0x4000, props.RamBase);

        ula.VideoMode = VideoMode.Mode6;
        props = ula.CurrentVideoModeProperties;
        Assert.Equal(320, props.Width);
        Assert.Equal(0x6000, props.RamBase);
    }

    [Fact]
    public void VideoMemoryBase_ReturnsCorrectAddress()
    {
        var ula = new ElectronUla();
        
        ula.VideoMode = VideoMode.Mode0;
        Assert.Equal(0x3000, ula.VideoMemoryBase);

        ula.VideoMode = VideoMode.Mode3;
        Assert.Equal(0x4000, ula.VideoMemoryBase);

        ula.VideoMode = VideoMode.Mode6;
        Assert.Equal(0x6000, ula.VideoMemoryBase);
    }

    [Theory]
    [InlineData((byte)VideoMode.Mode0, 0x3000)]
    [InlineData((byte)VideoMode.Mode1, 0x3000)]
    [InlineData((byte)VideoMode.Mode2, 0x3000)]
    [InlineData((byte)VideoMode.Mode3, 0x4000)]
    [InlineData((byte)VideoMode.Mode4, 0x5800)]
    [InlineData((byte)VideoMode.Mode5, 0x5800)]
    [InlineData((byte)VideoMode.Mode6, 0x6000)]
    public void AllModes_HaveCorrectBaseAddress(byte modeValue, ushort expectedBase)
    {
        var mode = (VideoMode)modeValue;
        var ula = new ElectronUla();
        ula.VideoMode = mode;
        Assert.Equal(expectedBase, ula.VideoMemoryBase);
    }

    [Fact]
    public void ModeSwitching_UpdatesVideoMemoryBase()
    {
        var ula = new ElectronUla();
        
        // Start in Mode 0
        Assert.Equal(0x3000, ula.VideoMemoryBase);

        // Switch to Mode 3 (different base)
        ula.VideoMode = VideoMode.Mode3;
        Assert.Equal(0x4000, ula.VideoMemoryBase);

        // Switch to Mode 4 (yet another base)
        ula.VideoMode = VideoMode.Mode4;
        Assert.Equal(0x5800, ula.VideoMemoryBase);

        // Back to Mode 0
        ula.VideoMode = VideoMode.Mode0;
        Assert.Equal(0x3000, ula.VideoMemoryBase);
    }

    [Fact]
    public void GraphicsModes_HaveDifferentColorDepths()
    {
        var mode0 = VideoModeProperties.GetMode(VideoMode.Mode0);
        var mode1 = VideoModeProperties.GetMode(VideoMode.Mode1);
        var mode2 = VideoModeProperties.GetMode(VideoMode.Mode2);

        // All graphics modes at 0x3000 should have different color depths
        Assert.Equal(1, mode0.BitsPerPixel);  // 2-colour
        Assert.Equal(2, mode1.BitsPerPixel);  // 4-colour
        Assert.Equal(4, mode2.BitsPerPixel);  // 16-colour
    }

    [Fact]
    public void TextModes_Have2Colours()
    {
        var mode3 = VideoModeProperties.GetMode(VideoMode.Mode3);
        var mode6 = VideoModeProperties.GetMode(VideoMode.Mode6);

        Assert.Equal(1, mode3.BitsPerPixel);  // 2-colour text
        Assert.Equal(1, mode6.BitsPerPixel);  // 2-colour text
    }

    [Fact]
    public void HighResolutionModes_Have640Width()
    {
        var mode0 = VideoModeProperties.GetMode(VideoMode.Mode0);  // 640×256
        var mode3 = VideoModeProperties.GetMode(VideoMode.Mode3);  // 640×200

        Assert.Equal(640, mode0.Width);
        Assert.Equal(640, mode3.Width);
    }

    [Fact]
    public void MediumResolutionModes_Have320Width()
    {
        var mode1 = VideoModeProperties.GetMode(VideoMode.Mode1);  // 320×256
        var mode4 = VideoModeProperties.GetMode(VideoMode.Mode4);  // 320×256
        var mode6 = VideoModeProperties.GetMode(VideoMode.Mode6);  // 320×200

        Assert.Equal(320, mode1.Width);
        Assert.Equal(320, mode4.Width);
        Assert.Equal(320, mode6.Width);
    }

    [Fact]
    public void LowResolutionModes_Have160Width()
    {
        var mode2 = VideoModeProperties.GetMode(VideoMode.Mode2);  // 160×256
        var mode5 = VideoModeProperties.GetMode(VideoMode.Mode5);  // 160×256

        Assert.Equal(160, mode2.Width);
        Assert.Equal(160, mode5.Width);
    }
}
