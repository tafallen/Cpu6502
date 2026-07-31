using Machines.Pet;
using Xunit;

namespace Machines.Pet.Tests;

public class PetKeyboardTests
{
    [Fact]
    public void Pia6520_KeyboardScan_ReadsSelectedColumnRowState()
    {
        byte[] romData = new byte[0x7000];
        var machine = new PetMachine(romData);

        // Press key at Column 3, Row 5
        machine.Keyboard.KeyDown(col: 3, row: 5);

        // Write column 3 to PIA Port A ($E810)
        machine.Pia.Write(0xE810, 0x03);

        // Read PIA Port B ($E812)
        byte rowState = machine.Pia.Read(0xE812);

        // Bit 5 should be 0 (pressed)
        Assert.Equal(unchecked((byte)~(1 << 5)), rowState);
    }
}
