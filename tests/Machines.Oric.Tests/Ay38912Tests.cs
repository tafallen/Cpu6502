using Machines.Oric;
using Xunit;

namespace Machines.Oric.Tests;

public class Ay38912Tests
{
    [Fact]
    public void Ay38912_Registers_ReadWriteAll16Registers()
    {
        var psg = new Ay38912();

        for (byte reg = 0; reg < 16; reg++)
        {
            psg.SelectRegister(reg);
            psg.WriteData((byte)(reg * 10));
            Assert.Equal((byte)(reg * 10), psg.ReadRegister());
        }
    }
}
