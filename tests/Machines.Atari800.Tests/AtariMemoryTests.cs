using Machines.Atari800;
using Xunit;

namespace Machines.Atari800.Tests;

public class AtariMemoryTests
{
    [Fact]
    public void MemoryBus_RamReadWrite_Succeeds()
    {
        var bus = new AtariBus();
        bus.Write(0x0200, 0x42);
        Assert.Equal(0x42, bus.Read(0x0200));
    }

    [Fact]
    public void Pia_PortB_BankSwitching_Works()
    {
        var bus = new AtariBus();

        // Enable OS ROM (PORTB bit 0 = 0)
        bus.Pia.Write(0x02, 0xFE);
        Assert.True(bus.OsRomEnabled);

        // Disable OS ROM (PORTB bit 0 = 1)
        bus.Pia.Write(0x02, 0xFF);
        Assert.False(bus.OsRomEnabled);
    }
}
