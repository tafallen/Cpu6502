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
    public void ValidateInitialization_Succeeds()
    {
        var m = new AtomMachine(MakeRom(), MakeRom());
        m.ValidateInitialization(); // should not throw
    }

}
