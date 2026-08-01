using Machines.Common;
using Xunit;

namespace Machines.Atari800.Tests;

public class PokeyTests
{
    [Fact]
    public void Pokey_ReadWriteRegisters_Works()
    {
        var pokey = new Pokey();
        pokey.Write(0x00, 0x55);
        Assert.Equal(0x55, pokey.Read(0x00));
    }

    [Fact]
    public void Pokey_KeyboardTrigger_FiresIrq()
    {
        var pokey = new Pokey();
        pokey.Write(0x0E, 0x01); // Enable Keyboard IRQ
        pokey.TriggerKeypress(0x2A);

        Assert.Equal(0x2A, pokey.Read(0x09)); // KBCODE
        Assert.True(pokey.Irq);
    }
}
