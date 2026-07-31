using Machines.Pet;
using Xunit;

namespace Machines.Pet.Tests;

public class Ieee488Tests
{
    [Fact]
    public void AutoLoadPrg_LoadsPayloadToTargetRamAddress()
    {
        byte[] romData = new byte[0x7000];
        var machine = new PetMachine(romData);

        byte[] prgData = [0x01, 0x04, 0xA9, 0x05, 0x60]; // Load at $0401: LDA #$05, RTS
        machine.Ieee488.AutoLoadPrg(prgData, machine.Ram);

        Assert.Equal(0xA9, machine.Ram.Read(0x0401));
        Assert.Equal(0x05, machine.Ram.Read(0x0402));
        Assert.Equal(0x60, machine.Ram.Read(0x0403));
    }
}
