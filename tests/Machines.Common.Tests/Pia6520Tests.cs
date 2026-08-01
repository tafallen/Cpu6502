using Machines.Common;
using Xunit;

namespace Machines.Common.Tests;

public class Pia6520Tests
{
    [Fact]
    public void Pia_ReadWriteControlAndPorts_Works()
    {
        var pia = new Pia6520();

        pia.Write(0x00, 0x12);
        pia.Write(0x01, 0x04);
        pia.Write(0x02, 0x34);
        pia.Write(0x03, 0x04);

        Assert.Equal(0x12, pia.PortA);
        Assert.Equal(0x04, pia.ControlA);
        Assert.Equal(0x34, pia.PortB);
        Assert.Equal(0x04, pia.ControlB);
    }
}
