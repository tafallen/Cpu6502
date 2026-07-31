using Cpu6502.Core;
using Machines.Common;

namespace Machines.Oric;

/// <summary>
/// High-performance Oric-1 / Oric Atmos ULA Video Hardware Renderer (240×200).
/// Optimized with direct RAM span access and fast scanline calculations.
/// </summary>
public sealed class OricUlaVideo
{
    public const int FrameWidth = 240;
    public const int FrameHeight = 200;

    private readonly uint[] _pixelBuffer = new uint[FrameWidth * FrameHeight];

    // Oric 8-color Teletext palette (ARGB32)
    private static readonly uint[] Palette =
    [
        0xFF000000, // 0 Black
        0xFFFF0000, // 1 Red
        0xFF00FF00, // 2 Green
        0xFFFFFF00, // 3 Yellow
        0xFF0000FF, // 4 Blue
        0xFFFF00FF, // 5 Magenta
        0xFF00FFFF, // 6 Cyan
        0xFFFFFFFF  // 7 White
    ];

    public void RenderFrame(Ram ram, IVideoSink sink)
    {
        ReadOnlySpan<byte> ramSpan = ram.DirectReadBuffer;
        bool isHiresMode = false;

        for (int y = 0; y < FrameHeight; y++)
        {
            uint ink = Palette[7];   // White
            uint paper = Palette[0]; // Black

            int textRow = y >> 3; // y / 8
            int scanLineInChar = y & 7; // y % 8
            int textRowOffset = 0xBB80 + textRow * 40;
            int hiresRowOffset = 0xA000 + y * 40;
            int dstIdx = y * FrameWidth;

            for (int col = 0; col < 40; col++)
            {
                ushort addr = (isHiresMode && y < 176) ? (ushort)(hiresRowOffset + col) : (ushort)(textRowOffset + col);
                byte val = ramSpan.Length > 0 ? ramSpan[addr] : ram.Read(addr);

                // Serial Attributes ($00–$1F)
                if ((val & 0x60) == 0x00)
                {
                    if (val <= 0x07)
                    {
                        ink = Palette[val & 0x07]; // Ink color
                    }
                    else if (val >= 0x10 && val <= 0x17)
                    {
                        paper = Palette[val & 0x07]; // Paper color
                    }
                    else if (val == 0x18)
                    {
                        isHiresMode = false; // TEXT mode
                    }
                    else if (val == 0x1A)
                    {
                        isHiresMode = true; // HIRES mode
                    }
                    val = 0x20; // Render attribute column as space
                }

                // Render 6 pixels per byte
                byte pixels;
                if (isHiresMode && y < 176)
                {
                    pixels = (byte)(val & 0x3F);
                }
                else
                {
                    int fontAddr = 0xB400 + (val & 0x7F) * 8 + scanLineInChar;
                    pixels = (byte)((ramSpan.Length > 0 ? ramSpan[fontAddr] : ram.Read((ushort)fontAddr)) & 0x3F);
                }

                _pixelBuffer[dstIdx++] = (pixels & 0x20) != 0 ? ink : paper;
                _pixelBuffer[dstIdx++] = (pixels & 0x10) != 0 ? ink : paper;
                _pixelBuffer[dstIdx++] = (pixels & 0x08) != 0 ? ink : paper;
                _pixelBuffer[dstIdx++] = (pixels & 0x04) != 0 ? ink : paper;
                _pixelBuffer[dstIdx++] = (pixels & 0x02) != 0 ? ink : paper;
                _pixelBuffer[dstIdx++] = (pixels & 0x01) != 0 ? ink : paper;
            }
        }

        sink.SubmitFrame(_pixelBuffer, FrameWidth, FrameHeight);
    }
}
