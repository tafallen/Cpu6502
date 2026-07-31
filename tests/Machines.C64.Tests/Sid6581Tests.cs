using Machines.C64;
using Xunit;

namespace Machines.C64.Tests;

public class Sid6581Tests
{
    [Fact]
    public void VoiceFrequency_SetAndRead_ReturnsCorrectFrequency()
    {
        var sid = new Sid6581();

        // Write Voice 1 Frequency Low = 0x34, High = 0x12
        sid.Write(0x00, 0x34);
        sid.Write(0x01, 0x12);

        Assert.Equal(0x1234, sid.GetVoiceFrequency(0));
    }

    [Fact]
    public void VolumeAndFilterFlags_WriteRegister18_ParsesFlags()
    {
        var sid = new Sid6581();

        // Write Volume = 15, LowPass = true (0x1F)
        sid.Write(0x18, 0x1F);

        Assert.Equal(15, sid.Volume);
        Assert.True(sid.FilterLowPass);
        Assert.False(sid.FilterHighPass);
    }
}
