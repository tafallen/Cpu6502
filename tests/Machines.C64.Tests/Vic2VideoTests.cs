using Machines.C64;
using Xunit;

namespace Machines.C64.Tests;

public class Vic2VideoTests
{
    [Fact]
    public void BorderAndBackgroundColors_WriteAndRead_ReturnsValue()
    {
        var vic = new Vic2Video();

        // Write Border Color = Light Blue (14)
        vic.Write(0x20, 14);
        Assert.Equal(14, vic.BorderColor);

        // Write Background Color = Blue (6)
        vic.Write(0x21, 6);
        Assert.Equal(6, vic.BackgroundColor0);
    }

    [Fact]
    public void RasterIrq_TriggersWhenRasterLineMatches()
    {
        var vic = new Vic2Video();

        // Enable Raster IRQ (InterruptEnable bit 0 = 1)
        vic.Write(0x1A, 0x01);

        // Set Raster Compare Line = 10
        vic.Write(0x12, 10);

        Assert.False(vic.Irq);

        // Tick 10 cycles -> raster line reaches 10
        vic.Tick(10);

        Assert.True(vic.Irq);
        Assert.Equal(10, vic.CurrentRasterLine);

        // Acknowledge IRQ by writing bit 0 to $D019
        vic.Write(0x19, 0x01);

        Assert.False(vic.Irq);
    }
}
