using Machines.BbcMaster;
using Xunit;

namespace Machines.BbcMaster.Tests;

public class BbcMasterMemoryTests
{
    [Fact]
    public void Acccon_ReadWrite_TogglesFlagsCorrectly()
    {
        var acccon = new BbcMasterAcccon();

        // Write 0x02 (DisplayShadowSelect)
        acccon.Write(0xFE34, 0x02);

        Assert.True(acccon.DisplayShadowSelect);
        Assert.False(acccon.ExecuteShadowSelect);
        Assert.Equal(0x02, acccon.Read(0xFE34));
    }
}
