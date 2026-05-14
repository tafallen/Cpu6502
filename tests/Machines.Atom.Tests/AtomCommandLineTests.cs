using Host.Atom;

namespace Machines.Atom.Tests;

public class AtomCommandLineTests
{
    [Fact]
    public void Parse_SetsDebugKeys_WhenFlagProvided()
    {
        var options = AtomCommandLine.Parse(["--basic", "basic.rom", "--os", "os.rom", "--debug-keys"]);

        Assert.True(options.DebugKeys);
        Assert.Equal(3, options.Scale);
    }

    [Fact]
    public void Parse_DefaultsDebugKeysToFalse()
    {
        var options = AtomCommandLine.Parse(["--basic", "basic.rom", "--os", "os.rom"]);

        Assert.False(options.DebugKeys);
        Assert.False(options.Gdb);
        Assert.Equal(1234, options.GdbPort);
    }

    [Fact]
    public void Parse_SetsGdbOptions_WhenFlagsProvided()
    {
        var options = AtomCommandLine.Parse(["--basic", "basic.rom", "--os", "os.rom", "--gdb", "--gdb-port", "2345"]);

        Assert.True(options.Gdb);
        Assert.Equal(2345, options.GdbPort);
    }

    [Fact]
    public void Parse_ThrowsForMissingRequiredArguments()
    {
        Assert.Throws<ArgumentException>(() => AtomCommandLine.Parse(["--basic", "basic.rom"]));
    }

    [Fact]
    public void Parse_SmoothFlag_SetsSmoothTrue()
    {
        var options = AtomCommandLine.Parse(["--basic", "b.rom", "--os", "o.rom", "--smooth"]);
        Assert.True(options.Smooth);
    }

    [Fact]
    public void Parse_NoSmoothFlag_DefaultsFalse()
    {
        var options = AtomCommandLine.Parse(["--basic", "b.rom", "--os", "o.rom"]);
        Assert.False(options.Smooth);
    }

    [Fact]
    public void Parse_ScanlinesFlag_SetsScanlineIntensity()
    {
        var options = AtomCommandLine.Parse(["--basic", "b.rom", "--os", "o.rom", "--scanlines", "0.5"]);
        Assert.Equal(0.5f, options.ScanlineIntensity);
    }

    [Fact]
    public void Parse_NoScanlinesFlag_DefaultsToZero()
    {
        var options = AtomCommandLine.Parse(["--basic", "b.rom", "--os", "o.rom"]);
        Assert.Equal(0f, options.ScanlineIntensity);
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.1")]
    [InlineData("abc")]
    public void Parse_InvalidScanlines_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            AtomCommandLine.Parse(["--basic", "b.rom", "--os", "o.rom", "--scanlines", value]));
    }
}

