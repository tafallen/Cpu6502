using Machines.Common;
using Xunit;

namespace Machines.Common.Tests;

public class Wd1770FdcTests
{
    [Fact]
    public void Wd1770_ReadWriteRegisters_Succeeds()
    {
        var fdc = new Wd1770Fdc();

        fdc.Write(0x01, 0x0A); // Track reg
        fdc.Write(0x02, 0x01); // Sector reg
        fdc.Write(0x03, 0x55); // Data reg

        Assert.Equal(0x0A, fdc.Read(0x01));
        Assert.Equal(0x01, fdc.Read(0x02));
        Assert.Equal(0x55, fdc.Read(0x03));
    }

    [Fact]
    public void Wd1770_SeekCommand_UpdatesTrack()
    {
        var fdc = new Wd1770Fdc();
        fdc.Write(0x03, 0x14); // Data reg target track = 20
        fdc.Write(0x00, 0x10); // Seek command

        Assert.Equal(0x14, fdc.Read(0x01)); // Track reg updated to 20
    }
}
