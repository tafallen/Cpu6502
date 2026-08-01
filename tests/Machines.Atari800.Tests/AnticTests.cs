using Machines.Atari800;
using Xunit;

namespace Machines.Atari800.Tests;

public class AnticTests
{
    [Fact]
    public void Antic_DlistAddress_ReadsWrites()
    {
        var antic = new Antic();
        antic.Write(0x02, 0x00);
        antic.Write(0x03, 0x40);

        Assert.Equal(0x4000, antic.DlistAddress);
    }

    [Fact]
    public void Antic_VblankNmi_TriggersNmi()
    {
        var antic = new Antic();
        antic.Write(0x0E, 0x40); // Enable VBLANK NMI
        antic.Tick(248);

        Assert.True(antic.Nmi);
    }
}
