using Machines.Atari800;
using Xunit;

namespace Machines.Atari800.Tests;

public class AtariLoaderTests
{
    [Fact]
    public void LoadXex_ParsesBinaryHeader_LoadsRAM()
    {
        var bus = new AtariBus();
        byte[] xex = new byte[]
        {
            0xFF, 0xFF,             // Header
            0x00, 0x06,             // Start $0600
            0x03, 0x06,             // End $0603
            0xA9, 0x01, 0x85, 0x80  // LDA #$01, STA $80
        };

        ushort runAddr = AtariProgramLoader.LoadXex(xex, bus);
        Assert.Equal(0x0600, runAddr);
        Assert.Equal(0xA9, bus.Ram.Read(0x0600));
        Assert.Equal(0x85, bus.Ram.Read(0x0602));
    }
}
