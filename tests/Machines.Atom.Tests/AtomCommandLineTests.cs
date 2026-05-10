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
}

