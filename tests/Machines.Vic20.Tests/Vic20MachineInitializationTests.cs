using Machines.Vic20;
using Machines.Common;
using Xunit;

namespace Machines.Vic20.Tests;

public class Vic20MachineInitializationTests
{
    private static byte[] MakeRom(int size = 0x2000)
    {
        var rom = new byte[size];
        Array.Fill(rom, (byte)0xEA);
        return rom;
    }

    [Fact]
    public void ValidateInitialization_HappyPath_Succeeds()
    {
        var m = new Vic20Machine(MakeRom(), MakeRom());
        m.ValidateInitialization(); // should not throw
    }

    [Fact]
    public void ValidateInitialization_ChecksCpu()
    {
        var m = new Vic20Machine(MakeRom(), MakeRom());
        Assert.NotNull(m.Cpu);
    }

    [Fact]
    public void ValidateInitialization_ChecksRam()
    {
        var m = new Vic20Machine(MakeRom(), MakeRom());
        Assert.NotNull(m.Ram);
        Assert.NotEmpty(m.Ram.Memory.ToArray());
    }

    [Fact]
    public void ValidateInitialization_ChecksVia1()
    {
        var m = new Vic20Machine(MakeRom(), MakeRom());
        Assert.NotNull(m.Via1);
    }

    [Fact]
    public void ValidateInitialization_ChecksVia2()
    {
        var m = new Vic20Machine(MakeRom(), MakeRom());
        Assert.NotNull(m.Via2);
    }

    [Fact]
    public void ValidateInitialization_ChecksBus()
    {
        var m = new Vic20Machine(MakeRom(), MakeRom());
        Assert.NotNull(m.Bus);
    }
}
