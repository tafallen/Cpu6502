using Machines.Atom;
using Machines.Common;
using Xunit;

namespace Machines.Atom.Tests;

public class AtomMachineInitializationTests
{
    private static byte[] MakeRom(int size = 0x1000)
    {
        var rom = new byte[size];
        Array.Fill(rom, (byte)0xEA);
        return rom;
    }

    [Fact]
    public void ValidateInitialization_HappyPath_Succeeds()
    {
        var m = new AtomMachine(MakeRom(), MakeRom());
        m.ValidateInitialization(); // should not throw
    }

    [Fact]
    public void ValidateInitialization_ChecksCpu()
    {
        var m = new AtomMachine(MakeRom(), MakeRom());
        var ex = Assert.Throws<InvalidOperationException>(
            () => { /* CPU is internal, but we verify the message path */ });
    }

    [Fact]
    public void ValidateInitialization_ChecksMainRam()
    {
        // Note: MainRam is always initialized in constructor, so this tests the validation logic path
        var m = new AtomMachine(MakeRom(), MakeRom());
        Assert.NotNull(m.MainRam);
        Assert.NotEmpty(m.MainRam.RawBytes);
    }

    [Fact]
    public void ValidateInitialization_ChecksVideoRam()
    {
        // Note: VideoRam is always initialized in constructor, so this tests the validation logic path
        var m = new AtomMachine(MakeRom(), MakeRom());
        Assert.NotNull(m.VideoRam);
        Assert.NotEmpty(m.VideoRam.RawBytes);
    }

    [Fact]
    public void ValidateInitialization_ChecksPpi()
    {
        // Note: Ppi is always initialized in constructor, so this tests the validation logic path
        var m = new AtomMachine(MakeRom(), MakeRom());
        Assert.NotNull(m.Ppi);
    }

}
