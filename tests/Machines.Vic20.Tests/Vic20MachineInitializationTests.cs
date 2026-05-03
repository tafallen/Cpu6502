using Machines.Common;
using Machines.Vic20;
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
    public void ValidateInitialization_Succeeds()
    {
        var m = new Vic20Machine(MakeRom(), MakeRom());
        m.ValidateInitialization(); // should not throw
    }

}
