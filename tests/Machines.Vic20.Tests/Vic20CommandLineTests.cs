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
}

