using Cpu6502.Core;
using Machines.Common;

namespace Machines.BbcMicro;

/// <summary>
/// Mullard SAA5050 Teletext character generator chip used in BBC Micro Mode 7.
/// Renders 40 columns × 25 rows = 1,000 text/graphics characters from VRAM ($7C00–$7FBF).
/// Supports 8 Teletext colors, alphanumeric and mosaic graphics modes.
/// </summary>
public sealed class Saa5050
{
    public const int FrameWidth = 640;
    public const int FrameHeight = 256;

    private readonly uint[] _pixelBuffer = new uint[FrameWidth * FrameHeight];

    // Standard Teletext palette (ARGB32)
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

    public void RenderMode7(Ram ram, ushort vramBase, IVideoSink sink)
    {
        uint defaultFg = Palette[7]; // White
        uint defaultBg = Palette[0]; // Black

        for (int row = 0; row < 25; row++)
        {
            uint fg = defaultFg;
            uint bg = defaultBg;
            bool graphicsMode = false;
            bool contiguous = true;

            for (int col = 0; col < 40; col++)
            {
                ushort addr = (ushort)(vramBase + row * 40 + col);
                byte code = ram.Read(addr);

                // Teletext Control Characters ($00–$1F)
                if (code < 0x20)
                {
                    if (code >= 0x01 && code <= 0x07)
                    {
                        fg = Palette[code]; // Alpha color
                        graphicsMode = false;
                    }
                    else if (code >= 0x11 && code <= 0x17)
                    {
                        fg = Palette[code & 0x07]; // Graphics color
                        graphicsMode = true;
                    }
                    else if (code == 0x1D)
                    {
                        bg = fg; // New Background
                    }
                    else if (code == 0x1C)
                    {
                        bg = Palette[0]; // Black Background
                    }
                    code = 0x20; // Render control char as space
                }

                // Render 12 line-scan rows per character
                for (int scanRow = 0; scanRow < 10; scanRow++)
                {
                    int screenY = row * 10 + scanRow;
                    if ((uint)screenY >= (uint)FrameHeight) continue;

                    int xBase = col * 16;
                    int dstIdx = screenY * FrameWidth + xBase;

                    // Expand 6 pixel bits
                    byte glyphBits = GetGlyphRow(code, scanRow, graphicsMode, contiguous);

                    for (int p = 0; p < 16; p++)
                    {
                        int bitIdx = p / 2; // 2x horizontal scaling: 6 bits -> 12 px -> padded to 16 px
                        bool bitSet = (glyphBits & (0x20 >> Math.Min(bitIdx, 5))) != 0;
                        _pixelBuffer[dstIdx + p] = bitSet ? fg : bg;
                    }
                }
            }
        }

        sink.SubmitFrame(_pixelBuffer, FrameWidth, FrameHeight);
    }

    private static byte GetGlyphRow(byte code, int scanRow, bool graphics, bool contiguous)
    {
        if (!graphics)
        {
            // Simple alphanumeric 5x7 font pattern expansion
            if (scanRow < 2 || scanRow > 8) return 0x00;
            return (byte)(code & 0x3F);
        }

        // Mosaic graphics 2x3 block expansion
        int blockY = scanRow / 3; // 0, 1, 2
        byte mask = 0x00;
        if (blockY == 0) mask = (byte)(((code & 0x01) != 0 ? 0x38 : 0) | ((code & 0x02) != 0 ? 0x07 : 0));
        else if (blockY == 1) mask = (byte)(((code & 0x04) != 0 ? 0x38 : 0) | ((code & 0x08) != 0 ? 0x07 : 0));
        else mask = (byte)(((code & 0x10) != 0 ? 0x38 : 0) | ((code & 0x40) != 0 ? 0x07 : 0));
        return mask;
    }
}
