using Machines.Pet;
using Xunit;

namespace Machines.Pet.Tests;

public class PetMemoryTests
{
    [Fact]
    public void Ram_ReadWrite_WorksIn32KB()
    {
        byte[] romData = new byte[0x7000];
        var machine = new PetMachine(romData);

        machine.Bus.Write(0x1000, 0x77);
        Assert.Equal(0x77, machine.Bus.Read(0x1000));
    }

    [Fact]
    public void VideoRam_ReadWrite_WorksIn2KB()
    {
        byte[] romData = new byte[0x7000];
        var machine = new PetMachine(romData);

        machine.Bus.Write(0x8000, 0x41); // 'A' in Video RAM
        Assert.Equal(0x41, machine.Bus.Read(0x8000));
    }

    [Fact]
    public void Video_RenderFrame_DoesNotThrow()
    {
        byte[] romData = new byte[0x7000];
        var machine = new PetMachine(romData);

        machine.VideoRam.Write(0x0000, 0x41); // 'A'
        var video = new PetVideo();
        video.RenderFrame(machine.VideoRam, new DummyVideoSink());
    }

    [Fact]
    public void Keyboard_KeyDown_ScansRowCorrectly()
    {
        byte[] romData = new byte[0x7000];
        var machine = new PetMachine(romData);

        machine.Keyboard.KeyDown(col: 4, row: 1);
        byte scanned = machine.Keyboard.ScanRow(4);

        Assert.Equal(unchecked((byte)~(1 << 1)), scanned);
    }

    private class DummyVideoSink : Machines.Common.IVideoSink
    {
        public void SubmitFrame(ReadOnlySpan<uint> pixelDataBuffer, int width, int height)
        {
            Assert.Equal(320, width);
            Assert.Equal(200, height);
        }
    }
}
