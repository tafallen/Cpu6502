using Machines.Pet;
using Xunit;

namespace Machines.Pet.Tests;

public class PetMemoryTests
{
    [Fact]
    public void Ram_ReadWrite_WorksIn32KB()
    {
        byte[] romData = new byte[0x7000];
        var machine = new PetMachine(romData);

        machine.Bus.Write(0x1000, 0x77);
        Assert.Equal(0x77, machine.Bus.Read(0x1000));
    }

    [Fact]
    public void VideoRam_ReadWrite_WorksIn2KB()
    {
        byte[] romData = new byte[0x7000];
        var machine = new PetMachine(romData);

        machine.Bus.Write(0x8000, 0x41); // 'A' in Video RAM
        Assert.Equal(0x41, machine.Bus.Read(0x8000));
    }
}
