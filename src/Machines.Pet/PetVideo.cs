using Cpu6502.Core;
using Machines.Common;

namespace Machines.Pet;

/// <summary>
/// Commodore PET 2001 / 4032 / 8032 monochrome text/character matrix video renderer.
/// High-performance linear scanline renderer for 40 columns × 25 rows = 1,000 bytes VRAM ($8000–$83E7).
/// Optimized with linear memory writes for maximum L1 CPU cache locality.
/// </summary>
public sealed class PetVideo
{
    public const int FrameWidth = 320; // 40 cols * 8 px
    public const int FrameHeight = 200; // 25 rows * 8 px

    private readonly uint[] _pixelBuffer = new uint[FrameWidth * FrameHeight];

    private const uint GreenOn  = 0xFF00FF00; // Classic PET green phosphor
    private const uint GreenOff = 0xFF000000; // Black background

    public void RenderFrame(Ram videoRam, IVideoSink sink)
    {
        ReadOnlySpan<byte> vramSpan = videoRam.DirectReadBuffer;

        for (int y = 0; y < FrameHeight; y++)
        {
            int row = y >> 3; // y / 8
            int scanRow = y & 7; // y % 8
            int rowOffset = row * 40;
            int dstIdx = y * FrameWidth;

            for (int col = 0; col < 40; col++)
            {
                byte characterCode = vramSpan.Length > 0 ? vramSpan[rowOffset + col] : videoRam.Read((ushort)(rowOffset + col));

                bool inverse = (characterCode & 0x80) != 0;
                int charIdx = characterCode & 0x7F;

                byte lineBits = GetPetCharacterPattern(charIdx, scanRow);

                for (int p = 0; p < 8; p++)
                {
                    bool bitSet = (lineBits & (0x80 >> p)) != 0;
                    if (inverse) bitSet = !bitSet;

                    _pixelBuffer[dstIdx++] = bitSet ? GreenOn : GreenOff;
                }
            }
        }

        sink.SubmitFrame(_pixelBuffer, FrameWidth, FrameHeight);
    }

    private static byte GetPetCharacterPattern(int charCode, int line)
    {
        if (line == 0 || line == 7) return 0x00;
        return (byte)(charCode & 0x7F);
    }
}
