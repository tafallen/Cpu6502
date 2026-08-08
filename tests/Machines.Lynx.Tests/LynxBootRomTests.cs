using System.IO;
using Cpu6502.Core;
using Machines.Common;
using Machines.Lynx;
using Xunit;
using Xunit.Abstractions;

namespace Machines.Lynx.Tests;

public class LynxBootRomTests
{
    private readonly ITestOutputHelper _output;

    public LynxBootRomTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void LynxMachine_DumpScreenBuffer_OutputsAsciiArt()
    {
        string bootRomPath = "../../../../../roms/Atari Lynx/lynxboot.img";
        string gamePath = "../../../../../roms/Atari Lynx/Games/California Games (1991).lnx";

        byte[]? bootRom = File.Exists(bootRomPath) ? File.ReadAllBytes(bootRomPath) : null;
        byte[]? gameCart = File.Exists(gamePath) ? File.ReadAllBytes(gamePath) : null;

        if (gameCart is not null)
        {
            var machine = new LynxMachine(cartridgeRom: gameCart);
            var sink = new TestVideoSink();

            machine.Reset();

            // Run 60 frames (1.2 seconds of execution)
            for (int i = 0; i < 60; i++)
            {
                machine.RunFrame(sink);
            }

            _output.WriteLine($"Frames submitted: {sink.FramesSubmitted}");
            _output.WriteLine($"Non-zero pixel count: {sink.NonZeroCount}");
            _output.WriteLine("ASCII Frame Dump (downsampled 40x25):");
            _output.WriteLine(sink.GetAsciiArt());
        }
    }

    private class TestVideoSink : IVideoSink
    {
        public int FramesSubmitted { get; private set; }
        public int NonZeroCount { get; private set; }
        private uint[] _lastFrame = new uint[160 * 102];

        public void SubmitFrame(ReadOnlySpan<uint> pixelBuffer, int width, int height)
        {
            FramesSubmitted++;
            NonZeroCount = 0;
            for (int i = 0; i < pixelBuffer.Length; i++)
            {
                _lastFrame[i] = pixelBuffer[i];
                if ((pixelBuffer[i] & 0x00FFFFFF) != 0)
                {
                    NonZeroCount++;
                }
            }
        }

        public string GetAsciiArt()
        {
            var sb = new System.Text.StringBuilder();
            // Downsample 160x102 screen to 40x25 characters
            for (int y = 0; y < 102; y += 4)
            {
                for (int x = 0; x < 160; x += 4)
                {
                    uint color = _lastFrame[y * 160 + x];
                    byte r = (byte)((color >> 16) & 0xFF);
                    byte g = (byte)((color >> 8) & 0xFF);
                    byte b = (byte)(color & 0xFF);
                    int brightness = (r + g + b) / 3;

                    char c = brightness switch
                    {
                        > 200 => '#',
                        > 150 => 'O',
                        > 100 => '*',
                        > 50  => '.',
                        _     => ' '
                    };
                    sb.Append(c);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
