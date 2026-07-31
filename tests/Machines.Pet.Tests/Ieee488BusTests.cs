using Machines.Pet;
using Xunit;

namespace Machines.Pet.Tests;

public class Ieee488BusTests
{
    [Fact]
    public void Ieee488_HandshakeSequence_AssertsLinesCorrectly()
    {
        var bus = new Ieee488Bus();

        bus.AssertAttention(true);
        Assert.True(bus.AtnLine);

        bus.WriteData(0x28); // Listener address 8
        Assert.True(bus.DavLine);
        Assert.Equal(0x28, bus.DataBus);

        bus.AcknowledgeData();
        Assert.False(bus.NdacLine);
        Assert.True(bus.NrfdLine);
    }
}
