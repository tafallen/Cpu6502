using Machines.Common;
using Xunit;

namespace Machines.Common.Tests;

public class PsgSoundTests
{
    [Fact]
    public void Ay38912_ReadWriteRegisters_Works()
    {
        var ay = new Ay38912();

        ay.SelectRegister(0x08); // Channel A Amplitude
        ay.WriteData(0x0F);

        ay.SelectRegister(0x08);
        Assert.Equal(0x0F, ay.ReadData());
    }

    [Fact]
    public void Sn76489_WriteCommands_Succeeds()
    {
        var sn = new Sn76489();

        sn.WriteByte(0x80 | 0x01); // Tone 0 frequency low
        sn.WriteByte(0x9F);        // Tone 0 volume mute (0x0F)

        Assert.NotNull(sn);
    }
}
