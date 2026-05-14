using Host.Vic20;

namespace Machines.Vic20.Tests;

public class Vic20CommandLineTests
{
    [Fact]
    public void Parse_SetsDebugKeys_WhenFlagProvided()
    {
        var options = Vic20CommandLine.Parse(["--basic", "basic.rom", "--kernal", "kernal.rom", "--debug-keys"]);

        Assert.True(options.DebugKeys);
        Assert.Equal(3, options.Scale);
    }

    [Fact]
    public void Parse_DefaultsDebugKeysToFalse()
    {
        var options = Vic20CommandLine.Parse(["--basic", "basic.rom", "--kernal", "kernal.rom"]);

        Assert.False(options.DebugKeys);
        Assert.False(options.Gdb);
        Assert.Equal(1234, options.GdbPort);
    }

    [Fact]
    public void Parse_SetsGdbOptions_WhenFlagsProvided()
    {
        var options = Vic20CommandLine.Parse(["--basic", "basic.rom", "--kernal", "kernal.rom", "--gdb", "--gdb-port", "2345"]);

        Assert.True(options.Gdb);
        Assert.Equal(2345, options.GdbPort);
    }

    [Fact]
    public void Parse_ThrowsForMissingRequiredArguments()
    {
        Assert.Throws<ArgumentException>(() => Vic20CommandLine.Parse(["--basic", "basic.rom"]));
    }

    [Fact]
    public void Parse_SmoothFlag_SetsSmoothTrue()
    {
        var options = Vic20CommandLine.Parse(["--basic", "b.rom", "--kernal", "k.rom", "--smooth"]);
        Assert.True(options.Smooth);
    }

    [Fact]
    public void Parse_NoSmoothFlag_DefaultsFalse()
    {
        var options = Vic20CommandLine.Parse(["--basic", "b.rom", "--kernal", "k.rom"]);
        Assert.False(options.Smooth);
    }

    [Fact]
    public void Parse_ScanlinesFlag_SetsScanlineIntensity()
    {
        var options = Vic20CommandLine.Parse(["--basic", "b.rom", "--kernal", "k.rom", "--scanlines", "0.3"]);
        Assert.Equal(0.3f, options.ScanlineIntensity);
    }

    [Fact]
    public void Parse_NoScanlinesFlag_DefaultsToZero()
    {
        var options = Vic20CommandLine.Parse(["--basic", "b.rom", "--kernal", "k.rom"]);
        Assert.Equal(0f, options.ScanlineIntensity);
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.1")]
    [InlineData("abc")]
    public void Parse_InvalidScanlines_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Vic20CommandLine.Parse(["--basic", "b.rom", "--kernal", "k.rom", "--scanlines", value]));
    }
}

