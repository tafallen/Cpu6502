using Machines.Atari800;
using Xunit;

namespace Machines.Atari800.Tests;

public class GtiaTests
{
    [Fact]
    public void Gtia_ColorPalette_ReadsRegisters()
    {
        var gtia = new Gtia();
        gtia.Write(0x16, 0x84);
        uint color = gtia.GetColor(0);
        Assert.NotEqual(0u, color);
    }
}
