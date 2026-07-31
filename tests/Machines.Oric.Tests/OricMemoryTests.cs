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

    [Fact]
    public void Keyboard_KeyDown_ScansColumnCorrectly()
    {
        byte[] osRom = new byte[0x4000];
        var machine = new OricMachine(osRom);

        machine.Keyboard.KeyDown(col: 3, row: 2);
        byte result = machine.Keyboard.ScanColumn(3);

        Assert.Equal(unchecked((byte)~(1 << 2)), result);
    }

    [Fact]
    public void Ay38912_SelectAndWriteRegister_Works()
    {
        var psg = new Ay38912();
        psg.SelectRegister(0); // R0 = Channel A Tone Fine
        psg.WriteData(0x80);

        psg.SelectRegister(8); // R8 = Channel A Volume
        psg.WriteData(0x0F);

        Assert.Equal(0x80, psg.GetChannelFrequency(0));
        Assert.Equal(15, psg.GetChannelVolume(0));
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
