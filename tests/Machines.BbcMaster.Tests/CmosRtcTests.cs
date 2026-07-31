using Machines.BbcMaster;
using Xunit;

namespace Machines.BbcMaster.Tests;

public class CmosRtcTests
{
    [Fact]
    public void Mc146818Rtc_SelectAndWriteRegister_Works()
    {
        var rtc = new Mc146818Rtc();

        // Select Register 0x14 (CMOS boot option byte)
        rtc.Write(0xFE30, 0x14);

        // Write 0xC5 to Data Register ($FE31)
        rtc.Write(0xFE31, 0xC5);

        // Verify reading back from Data Register ($FE31)
        Assert.Equal(0xC5, rtc.Read(0xFE31));
    }
}
