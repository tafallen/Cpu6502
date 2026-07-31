using Machines.Common;
using Machines.Pet;
using Xunit;

namespace Machines.Pet.Tests;

public class PetVideoTests
{
    [Fact]
    public void Video_RenderFrame_InverseAttributeFlipsPixels()
    {
        byte[] romData = new byte[0x7000];
        var machine = new PetMachine(romData);

        // Write inverse character 'A' ($81) to top-left of Video RAM ($8000)
        machine.VideoRam.Write(0x0000, 0x81);

        var video = new PetVideo();
        var sink = new TestVideoSink();
        video.RenderFrame(machine.VideoRam, sink);

        Assert.Equal(320, sink.LastWidth);
        Assert.Equal(200, sink.LastHeight);
    }

    private class TestVideoSink : IVideoSink
    {
        public int LastWidth { get; private set; }
        public int LastHeight { get; private set; }

        public void SubmitFrame(ReadOnlySpan<uint> pixelDataBuffer, int width, int height)
        {
            LastWidth = width;
            LastHeight = height;
        }
    }
}
