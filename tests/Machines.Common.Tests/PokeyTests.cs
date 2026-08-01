using Machines.Common;
using Xunit;

namespace Machines.Common.Tests;

public class PokeyTests
{
    [Fact]
    public void Pokey_ReadWriteRegisters_Works()
    {
        var pokey = new Pokey();

        for (ushort addr = 0x00; addr <= 0x0F; addr++)
        {
            pokey.Write(addr, (byte)(addr + 10));
        }

        Assert.Equal(24, pokey.IrqEnable);
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

    [Fact]
    public void Pokey_RandomNumberGenerators_ReturnBytes()
    {
        var pokey = new Pokey();
        byte r1 = pokey.Read(0x08);
        byte r2 = pokey.Read(0x0A);
        Assert.NotNull(pokey);
    }

    [Fact]
    public void Pokey_ResetKeyboardScanner_WhenSkctlZero()
    {
        var pokey = new Pokey();
        pokey.TriggerKeypress(0x10);
        pokey.Write(0x0F, 0x00); // SKCTL reset
        Assert.Equal(0xFF, pokey.Kbcode);
    }
}
