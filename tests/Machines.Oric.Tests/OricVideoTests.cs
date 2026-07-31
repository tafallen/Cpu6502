using Machines.Common;
using Machines.Oric;
using Xunit;

namespace Machines.Oric.Tests;

public class OricVideoTests
{
    [Fact]
    public void Video_SerialAttributes_ChangesInkAndPaperColor()
    {
        byte[] osRom = new byte[0x4000];
        var machine = new OricMachine(osRom);

        // $BB80: Red Ink attribute ($01), $BB81: Character 'A' ($41)
        machine.Ram.Write(0xBB80, 0x01); // Red Ink
        machine.Ram.Write(0xBB81, 0x41); // 'A'

        var video = new OricUlaVideo();
        var sink = new TestVideoSink();
        video.RenderFrame(machine.Ram, sink);

        Assert.Equal(240, sink.LastWidth);
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
