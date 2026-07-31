using Machines.C64;
using Xunit;

namespace Machines.C64.Tests;

public class C64MemoryTests
{
    [Fact]
    public void PortBits_DefaultState_MapsKernalAndBasicRoms()
    {
        var bus = new C64Bus();

        // Default PortData is 0x37 -> LORAM=1, HIRAM=1, CHAREN=1
        Assert.True(bus.Loram);
        Assert.True(bus.Hiram);
        Assert.True(bus.Charen);
        Assert.Equal(0x37, bus.Read(0x0001));
    }

    [Fact]
    public void PortBits_TogglingLoram_SwitchesBasicRomToRam()
    {
        var bus = new C64Bus();

        // Write 'B' (0x42) to BASIC ROM ($A000) and 'R' (0x52) to RAM ($A000)
        bus.BasicRom[0] = 0x42;
        bus.Ram.Write(0xA000, 0x52);

        // Default: BASIC ROM mapped -> reads 0x42
        Assert.Equal(0x42, bus.Read(0xA000));

        // Clear LORAM (bit 0 = 0) -> 0x36
        bus.Write(0x0001, 0x36);
        Assert.False(bus.Loram);

        // Should now read RAM at $A000 -> 0x52
        Assert.Equal(0x52, bus.Read(0xA000));
    }

    [Fact]
    public void CpuWrites_AlwaysModifyRam_EvenWhenRomIsMapped()
    {
        var bus = new C64Bus();

        // KERNAL ROM is mapped at $E000
        Assert.True(bus.Hiram);

        // Write to $E000
        bus.Write(0xE000, 0x99);

        // Underlying RAM at $E000 should contain 0x99
        Assert.Equal(0x99, bus.Ram.Read(0xE000));
    }
}
