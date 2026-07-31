using Cpu6502.Core;
using Machines.Common;

namespace Machines.Oric;

/// <summary>
/// Oric-1 / Oric Atmos ULA Video Hardware Renderer (240×200).
/// Supports TEXT mode ($BB80–$BFDF) and HIRES mode ($A000–$BF3F) with serial attributes.
/// Serial attribute bytes ($00–$1F) modify ink/paper color, blinking, and text/graphics mode.
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
        // Default to TEXT mode at $BB80 unless HIRES attribute encountered
        bool isHiresMode = false;

        for (int y = 0; y < FrameHeight; y++)
        {
            uint ink = Palette[7];   // White
            uint paper = Palette[0]; // Black

            int textRow = y / 8;
            int scanLineInChar = y % 8;

            for (int col = 0; col < 40; col++)
            {
                ushort addr;
                if (isHiresMode && y < 176)
                {
                    // HIRES bitmap mode ($A000 + y*40 + col)
                    addr = (ushort)(0xA000 + y * 40 + col);
                }
                else
                {
                    // TEXT mode ($BB80 + textRow*40 + col)
                    addr = (ushort)(0xBB80 + textRow * 40 + col);
                }

                byte val = ram.Read(addr);

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
                int dstX = col * 6;
                int dstIdx = y * FrameWidth + dstX;

                byte pixels;
                if (isHiresMode && y < 176)
                {
                    pixels = (byte)(val & 0x3F);
                }
                else
                {
                    // Character lookup from $B400 font table
                    ushort fontAddr = (ushort)(0xB400 + (val & 0x7F) * 8 + scanLineInChar);
                    pixels = (byte)(ram.Read(fontAddr) & 0x3F);
                }

                for (int p = 0; p < 6; p++)
                {
                    bool bitSet = (pixels & (0x20 >> p)) != 0;
                    _pixelBuffer[dstIdx + p] = bitSet ? ink : paper;
                }
            }
        }

        sink.SubmitFrame(_pixelBuffer, FrameWidth, FrameHeight);
    }
}
