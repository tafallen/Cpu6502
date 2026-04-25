using Machines.Common;

namespace Machines.Atom;

/// <summary>
/// Motorola MC6847 Video Display Generator.
/// Reads video RAM and renders a 256×192 ARGB32 frame to an IVideoSink.
/// Mode is controlled by the AG/GM0-GM2/CSS pins, wired from PPI Port C on the Atom.
/// </summary>
public sealed class Mc6847
{
    private readonly byte[] _vram;
    private readonly byte[] _charRom; // 64 chars × 12 rows = 768 bytes
    private readonly ushort _vramBase;

    public static readonly uint ColorGreen = 0xFF00CC00u;
    public static readonly uint ColorBuff  = 0xFFFFBE00u;
    public static readonly uint ColorBlack = 0xFF000000u;

    // 4-color palettes indexed by [cssIndex][2-bit color code]
    public static readonly uint[][] FourColorPalettes =
    [
        [ColorBlack, ColorGreen,     0xFFCCCC00u, 0xFF0000CCu], // CSS=0
        [ColorBlack, ColorBuff,      0xFF00CCCCu, 0xFFCC00CCu]  // CSS=1
    ];

    /// <summary>
    /// Control byte mirroring PPI Port C:
    /// bit 0 = AG, bits 3-1 = GM2:GM1:GM0 (as GM0=bit1, GM1=bit2, GM2=bit3), bit 4 = CSS.
    /// </summary>
    public byte Control { get; set; }

    public Mc6847(byte[] vram, byte[] charRom, ushort vramBase = 0x8000)
    {
        _vram = vram;
        _charRom = charRom;
        _vramBase = vramBase;
    }

    private bool IsGraphics => (Control & 0x01) != 0;
    private int  GmBits     => (Control >> 1) & 0x07; // GM2:GM1:GM0 packed from bits 3-1
    private bool Css        => (Control & 0x10) != 0;

    private byte VramRead(int offset) =>
        (offset >= 0 && offset < _vram.Length) ? _vram[offset] : (byte)0;

    public void RenderFrame(IVideoSink sink)
    {
        uint[] pixels = new uint[256 * 192];
        if (IsGraphics)
            RenderGraphics(pixels);
        else
            RenderAlphanumeric(pixels);
        sink.SubmitFrame(pixels, 256, 192);
    }

    // ── Alphanumeric mode (32×16 chars, 8×12 px each) ──────────────────────

    private void RenderAlphanumeric(uint[] pixels)
    {
        uint fg = Css ? ColorBuff : ColorGreen;
        uint bg = ColorBlack;

        for (int charRow = 0; charRow < 16; charRow++)
        {
            for (int charCol = 0; charCol < 32; charCol++)
            {
                byte data    = VramRead(charRow * 32 + charCol);
                bool inv     = (data & 0x80) != 0;
                int  charIdx = data & 0x3F;

                for (int pixRow = 0; pixRow < 12; pixRow++)
                {
                    byte bits = _charRom[charIdx * 12 + pixRow];
                    int  yOut = charRow * 12 + pixRow;

                    for (int pixCol = 0; pixCol < 8; pixCol++)
                    {
                        bool set = (bits & (0x80 >> pixCol)) != 0;
                        if (inv) set = !set;
                        pixels[yOut * 256 + charCol * 8 + pixCol] = set ? fg : bg;
                    }
                }
            }
        }
    }

    // ── Graphics modes ───────────────────────────────────────────────────────

    private void RenderGraphics(uint[] pixels)
    {
        // Graphics mode table indexed by GM2:GM1:GM0 (bits 3-1 of Control → GmBits)
        //   GM  srcW  srcH  bpp  scaleX  scaleY
        (int srcW, int srcH, int bpp) = GmBits switch
        {
            0 => (64,  64,  2), // CG1
            1 => (128, 64,  1), // RG1
            2 => (128, 64,  2), // CG2
            3 => (128, 96,  1), // RG2
            4 => (128, 96,  2), // CG3
            5 => (128, 192, 1), // RG3
            6 => (128, 192, 2), // CG6
            _ => (256, 192, 1), // RG6
        };

        int scaleX = 256 / srcW;
        int scaleY = 192 / srcH;

        if (bpp == 1)
            RenderTwoColor(pixels, srcW, srcH, scaleX, scaleY);
        else
            RenderFourColor(pixels, srcW, srcH, scaleX, scaleY);
    }

    private void RenderTwoColor(uint[] pixels, int srcW, int srcH, int scaleX, int scaleY)
    {
        uint fg = Css ? ColorBuff : ColorGreen;
        uint bg = ColorBlack;
        int bytesPerRow = srcW / 8;

        for (int sy = 0; sy < srcH; sy++)
        {
            for (int bx = 0; bx < bytesPerRow; bx++)
            {
                byte bits = VramRead(sy * bytesPerRow + bx);
                for (int bit = 0; bit < 8; bit++)
                {
                    bool set = (bits & (0x80 >> bit)) != 0;
                    uint color = set ? fg : bg;
                    int sx = bx * 8 + bit;
                    PlotScaled(pixels, sx, sy, scaleX, scaleY, color);
                }
            }
        }
    }

    private void RenderFourColor(uint[] pixels, int srcW, int srcH, int scaleX, int scaleY)
    {
        var palette = FourColorPalettes[Css ? 1 : 0];
        int pixPerByte = 4; // 2 bits per pixel
        int bytesPerRow = srcW / pixPerByte;

        for (int sy = 0; sy < srcH; sy++)
        {
            for (int bx = 0; bx < bytesPerRow; bx++)
            {
                byte bits = VramRead(sy * bytesPerRow + bx);
                for (int pix = 0; pix < 4; pix++)
                {
                    int colorIdx = (bits >> (6 - pix * 2)) & 0x03;
                    int sx = bx * 4 + pix;
                    PlotScaled(pixels, sx, sy, scaleX, scaleY, palette[colorIdx]);
                }
            }
        }
    }

    private static void PlotScaled(uint[] pixels, int sx, int sy, int scaleX, int scaleY, uint color)
    {
        int x0 = sx * scaleX;
        int y0 = sy * scaleY;
        for (int dy = 0; dy < scaleY; dy++)
            for (int dx = 0; dx < scaleX; dx++)
                pixels[(y0 + dy) * 256 + (x0 + dx)] = color;
    }
}
