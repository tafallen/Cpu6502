using System.IO;
using Cpu6502.Core;
using Machines.Common;
using Machines.Lynx;
using Xunit;
using Xunit.Abstractions;

namespace Machines.Lynx.Tests;

public class LynxFrameExporterTests
{
    private readonly ITestOutputHelper _output;

    public LynxFrameExporterTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ExportFrames_25_50_100_ToPpm()
    {
        string bootRomPath = "../../../../../roms/Atari Lynx/lynxboot.img";
        string gamePath = "../../../../../roms/Atari Lynx/Games/California Games (1991).lnx";

        byte[]? bootRom = File.Exists(bootRomPath) ? File.ReadAllBytes(bootRomPath) : null;
        byte[]? gameCart = File.Exists(gamePath) ? File.ReadAllBytes(gamePath) : null;

        Assert.NotNull(bootRom);
        Assert.NotNull(gameCart);

        var machine = new LynxMachine(cartridgeRom: gameCart, bootRom: bootRom);
        var sink = new PpmVideoSink(outputDirectory: "../../../../../docs/lynx_frames");

        machine.Reset();

        for (int frame = 1; frame <= 100; frame++)
        {
            machine.RunFrame(sink);
            if (frame == 25 || frame == 50 || frame == 100)
            {
                sink.SaveFrame(frame);
                _output.WriteLine($"Saved frame {frame} to docs/lynx_frames/frame_{frame:D3}.ppm");
            }
        }
    }

    private class PpmVideoSink : IVideoSink
    {
        private readonly string _outputDir;
        private readonly uint[] _lastBuffer = new uint[160 * 102];

        public PpmVideoSink(string outputDirectory)
        {
            _outputDir = outputDirectory;
            Directory.CreateDirectory(_outputDir);
        }

        public void SubmitFrame(ReadOnlySpan<uint> pixelBuffer, int width, int height)
        {
            pixelBuffer.CopyTo(_lastBuffer);
        }

        public void SaveFrame(int frameNumber)
        {
            string path = Path.Combine(_outputDir, $"frame_{frameNumber:D3}.ppm");
            using var writer = new StreamWriter(path);
            writer.WriteLine("P3");
            writer.WriteLine("160 102");
            writer.WriteLine("255");

            for (int y = 0; y < 102; y++)
            {
                for (int x = 0; x < 160; x++)
                {
                    uint argb = _lastBuffer[y * 160 + x];
                    byte r = (byte)((argb >> 16) & 0xFF);
                    byte g = (byte)((argb >> 8) & 0xFF);
                    byte b = (byte)(argb & 0xFF);
                    writer.Write($"{r} {g} {b} ");
                }
                writer.WriteLine();
            }
        }
    }
}
