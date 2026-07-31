using Cpu6502.Core;
using Machines.Common;

namespace Machines.Pet;

/// <summary>
/// Commodore PET 2001 / 4032 / 8032 monochrome text/character matrix video renderer.
/// Renders 40 columns × 25 rows = 1,000 bytes from Video RAM ($8000–$83E7).
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
        for (int row = 0; row < 25; row++)
        {
            for (int col = 0; col < 40; col++)
            {
                ushort addr = (ushort)(row * 40 + col);
                byte characterCode = videoRam.Read(addr);

                bool inverse = (characterCode & 0x80) != 0;
                int charIdx = characterCode & 0x7F;

                for (int scanRow = 0; scanRow < 8; scanRow++)
                {
                    int screenY = row * 8 + scanRow;
                    int screenX = col * 8;
                    int dstIdx = screenY * FrameWidth + screenX;

                    byte lineBits = GetPetCharacterPattern(charIdx, scanRow);

                    for (int p = 0; p < 8; p++)
                    {
                        bool bitSet = (lineBits & (0x80 >> p)) != 0;
                        if (inverse) bitSet = !bitSet;

                        _pixelBuffer[dstIdx + p] = bitSet ? GreenOn : GreenOff;
                    }
                }
            }
        }

        sink.SubmitFrame(_pixelBuffer, FrameWidth, FrameHeight);
    }

    private static byte GetPetCharacterPattern(int charCode, int line)
    {
        // Simple built-in 8x8 font pattern generator for PETSCII characters
        if (line == 0 || line == 7) return 0x00;
        return (byte)(charCode & 0x7F);
    }
}
