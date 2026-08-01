using Machines.Common;
using Xunit;

namespace Machines.Common.Tests;

public class Mc146818RtcTests
{
    [Fact]
    public void Rtc_NvramReadWrite_Succeeds()
    {
        var rtc = new Mc146818Rtc();

        // Write address 0x0E (NVRAM byte 0) via port $FE30
        rtc.Write(0xFE30, 0x0E);
        rtc.Write(0xFE31, 0xA5);

        rtc.Write(0xFE30, 0x0E);
        Assert.Equal(0xA5, rtc.Read(0xFE31));
    }
}
