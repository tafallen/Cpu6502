using Machines.Oric;
using Xunit;

namespace Machines.Oric.Tests;

public class OricMemoryTests
{
    [Fact]
    public void Ram_ReadWrite_WorksIn48KB()
    {
        byte[] osRom = new byte[0x4000];
        var machine = new OricMachine(osRom);

        machine.Bus.Write(0x2000, 0xA5);
        Assert.Equal(0xA5, machine.Bus.Read(0x2000));
    }

    [Fact]
    public void OsRom_AddressMapping_SubtractsOffset()
    {
        byte[] osRom = new byte[0x4000];
        osRom[0x3FFC] = 0x00; // Reset vector low
        osRom[0x3FFD] = 0xC0; // Reset vector high ($C000)

        var machine = new OricMachine(osRom);
        machine.Reset();

        Assert.Equal(0xC000, machine.Cpu.PC);
    }

    [Fact]
    public void Video_RenderFrame_DoesNotThrow()
    {
        byte[] osRom = new byte[0x4000];
        var machine = new OricMachine(osRom);

        // Write attribute for Green Ink at $BB80
        machine.Ram.Write(0xBB80, 0x02);

        var video = new OricUlaVideo();
        video.RenderFrame(machine.Ram, new DummyVideoSink());
    }

    private class DummyVideoSink : Machines.Common.IVideoSink
    {
        public void SubmitFrame(ReadOnlySpan<uint> pixelDataBuffer, int width, int height)
        {
            Assert.Equal(240, width);
            Assert.Equal(200, height);
        }
    }
}
