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

    [Fact]
    public void DisplayBuffer_SwitchesBetweenMainAndShadowRam()
    {
        byte[] dummyRom = new byte[0x4000];
        var machine = new BbcMasterMachine(dummyRom);

        // Write 'M' (0x4D) to Main RAM VRAM ($7C00) and 'S' (0x53) to Shadow RAM VRAM ($7C00)
        machine.MainRam.Write(0x7C00, 0x4D);
        machine.ShadowRam.Write(0x7C00, 0x53);

        // Default DisplayShadowSelect is false -> renders from Main RAM
        Assert.False(machine.Acccon.DisplayShadowSelect);

        // Select DisplayShadowSelect (bit 1)
        machine.Acccon.Write(0xFE34, 0x02);
        Assert.True(machine.Acccon.DisplayShadowSelect);
    }
}
