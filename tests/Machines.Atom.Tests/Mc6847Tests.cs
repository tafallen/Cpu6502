using Machines.Common;

namespace Machines.Atom.Tests;

public class Mc6847Tests
{
    // Minimal character ROM: 64 chars × 12 rows.
    // All entries are 0 (blank) except char 1 which is all-ones (solid block).
    private static readonly byte[] TestCharRom = BuildTestCharRom();

    private static byte[] BuildTestCharRom()
    {
        var rom = new byte[64 * 12];
        for (int row = 0; row < 12; row++)
            rom[1 * 12 + row] = 0xFF; // char 1 = solid block
        return rom;
    }

    private readonly byte[] _vram = new byte[0x2000]; // 8KB
    private Mc6847 MakeVdg() => new(_vram, TestCharRom);

    private sealed class CaptureSink : IVideoSink
    {
        public uint[]? Pixels;
        public int Width, Height;
        public void SubmitFrame(ReadOnlySpan<uint> pixels, int width, int height)
        {
            Pixels = pixels.ToArray();
            Width = width;
            Height = height;
        }
    }

    // --- frame dimensions ---

    [Fact]
    public void AlphanumericMode_SubmitsFrame_256x192()
    {
        var sink = new CaptureSink();
        MakeVdg().RenderFrame(sink);
        Assert.Equal(256, sink.Width);
        Assert.Equal(192, sink.Height);
        Assert.Equal(256 * 192, sink.Pixels!.Length);
    }

    [Fact]
    public void GraphicsMode_SubmitsFrame_256x192()
    {
        var vdg = MakeVdg();
        vdg.Control = 0x01; // AG=1
        var sink = new CaptureSink();
        vdg.RenderFrame(sink);
        Assert.Equal(256, sink.Width);
        Assert.Equal(192, sink.Height);
    }

    // --- alphanumeric mode rendering ---

    [Fact]
    public void AlphanumericMode_AllZeroVram_AllPixelsAreBackground()
    {
        var sink = new CaptureSink();
        MakeVdg().RenderFrame(sink);
        uint bg = Mc6847.ColorBlack;
        Assert.All(sink.Pixels!, p => Assert.Equal(bg, p));
    }

    [Fact]
    public void AlphanumericMode_Char1AtOrigin_ProducesSolidBlockInFirstCell()
    {
        _vram[0] = 0x01; // char 1 at column 0, row 0
        var sink = new CaptureSink();
        MakeVdg().RenderFrame(sink);

        // First cell occupies pixels x=0..7, y=0..11
        uint fg = Mc6847.ColorGreen;
        for (int y = 0; y < 12; y++)
            for (int x = 0; x < 8; x++)
                Assert.Equal(fg, sink.Pixels![y * 256 + x]);
    }

    [Fact]
    public void AlphanumericMode_Char0Elsewhere_PixelsAreBackground()
    {
        _vram[0] = 0x01;
        var sink = new CaptureSink();
        MakeVdg().RenderFrame(sink);

        // Second cell (x=8..15, y=0..11) should be background (char 0 = blank)
        uint bg = Mc6847.ColorBlack;
        for (int y = 0; y < 12; y++)
            for (int x = 8; x < 16; x++)
                Assert.Equal(bg, sink.Pixels![y * 256 + x]);
    }

    [Fact]
    public void AlphanumericMode_Char1AtSecondColumn_RendersAtCorrectOffset()
    {
        _vram[1] = 0x01; // column 1, row 0
        var sink = new CaptureSink();
        MakeVdg().RenderFrame(sink);

        uint fg = Mc6847.ColorGreen;
        for (int y = 0; y < 12; y++)
            for (int x = 8; x < 16; x++)
                Assert.Equal(fg, sink.Pixels![y * 256 + x]);
    }

    [Fact]
    public void AlphanumericMode_Char1AtSecondRow_RendersAtCorrectOffset()
    {
        _vram[32] = 0x01; // column 0, row 1
        var sink = new CaptureSink();
        MakeVdg().RenderFrame(sink);

        uint fg = Mc6847.ColorGreen;
        for (int y = 12; y < 24; y++)
            for (int x = 0; x < 8; x++)
                Assert.Equal(fg, sink.Pixels![y * 256 + x]);
    }

    [Fact]
    public void AlphanumericMode_InverseBit_SwapsForegroundAndBackground()
    {
        _vram[0] = 0x81; // INV=1, char 1 (solid block → should render as background colour)
        var sink = new CaptureSink();
        MakeVdg().RenderFrame(sink);

        // Solid char with INV set: all pixels in the cell become the background colour
        uint bg = Mc6847.ColorBlack;
        for (int y = 0; y < 12; y++)
            for (int x = 0; x < 8; x++)
                Assert.Equal(bg, sink.Pixels![y * 256 + x]);
    }

    [Fact]
    public void AlphanumericMode_CSS1_UsesBuff()
    {
        _vram[0] = 0x01;
        var vdg = MakeVdg();
        vdg.Control = 0x10; // CSS=1, AG=0
        var sink = new CaptureSink();
        vdg.RenderFrame(sink);

        uint fg = Mc6847.ColorBuff;
        Assert.Equal(fg, sink.Pixels![0]); // top-left pixel of solid char 1
    }

    // --- RG6 graphics mode (256×192, 2-color, 6144 bytes) ---

    [Fact]
    public void RG6Mode_AllZeroVram_AllPixelsAreBackground()
    {
        var vdg = MakeVdg();
        vdg.Control = 0x0F; // AG=1, GM=111 (RG6)
        var sink = new CaptureSink();
        vdg.RenderFrame(sink);
        uint bg = Mc6847.ColorBlack;
        Assert.All(sink.Pixels!, p => Assert.Equal(bg, p));
    }

    [Fact]
    public void RG6Mode_FirstByteAllOnes_First8PixelsAreForeground()
    {
        _vram[0] = 0xFF;
        var vdg = MakeVdg();
        vdg.Control = 0x0F; // RG6
        var sink = new CaptureSink();
        vdg.RenderFrame(sink);

        uint fg = Mc6847.ColorGreen;
        for (int x = 0; x < 8; x++)
            Assert.Equal(fg, sink.Pixels![x]);
    }

    [Fact]
    public void RG6Mode_CSS1_UsesBuff()
    {
        _vram[0] = 0xFF;
        var vdg = MakeVdg();
        vdg.Control = 0x1F; // AG=1, GM=111 (RG6), CSS=1
        var sink = new CaptureSink();
        vdg.RenderFrame(sink);

        uint fg = Mc6847.ColorBuff;
        Assert.Equal(fg, sink.Pixels![0]);
    }

    // --- RG3 graphics mode (128×192, 2-color, 3072 bytes, 2× horizontal scale) ---

    [Fact]
    public void RG3Mode_FirstByteAllOnes_First16PixelsAreForeground()
    {
        _vram[0] = 0xFF;
        var vdg = MakeVdg();
        vdg.Control = 0x0B; // AG=1, GM=101 (RG3)
        var sink = new CaptureSink();
        vdg.RenderFrame(sink);

        uint fg = Mc6847.ColorGreen;
        for (int x = 0; x < 16; x++) // 8 pixels × 2× scale
            Assert.Equal(fg, sink.Pixels![x]);
    }

    // --- RG1 graphics mode (128×64, 2-color, 2× horiz × 3× vert scale) ---

    [Fact]
    public void RG1Mode_FirstByteAllOnes_ProducesScaledForeground()
    {
        _vram[0] = 0xFF;
        var vdg = MakeVdg();
        vdg.Control = 0x03; // AG=1, GM=001 (RG1)
        var sink = new CaptureSink();
        vdg.RenderFrame(sink);

        uint fg = Mc6847.ColorGreen;
        // First byte → 8 source pixels × 2× horiz = 16 output pixels, spanning 3 output rows
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 16; x++)
                Assert.Equal(fg, sink.Pixels![y * 256 + x]);
    }

    // --- CG1 graphics mode (64×64, 4-color, 4× horiz × 3× vert scale) ---

    [Fact]
    public void CG1Mode_FirstByteSet_ProducesFourColorPixels()
    {
        // CG1: 2 bits per pixel, 4 pixels per byte
        // 0b11_10_01_00 = 0xE4 → pixels: color3, color2, color1, color0
        _vram[0] = 0xE4;
        var vdg = MakeVdg();
        vdg.Control = 0x01; // AG=1, GM=000 (CG1)
        var sink = new CaptureSink();
        vdg.RenderFrame(sink);

        // 4 source pixels × 4× horiz = 16 output pixels per source row; 3× vert
        // CSS=0 palette: 00=black, 01=green, 10=yellow, 11=blue
        var palette = Mc6847.FourColorPalettes[0];
        for (int y = 0; y < 3; y++)
        {
            Assert.Equal(palette[3], sink.Pixels![y * 256 + 0]);  // bits 7-6 = 11
            Assert.Equal(palette[2], sink.Pixels![y * 256 + 4]);  // bits 5-4 = 10
            Assert.Equal(palette[1], sink.Pixels![y * 256 + 8]);  // bits 3-2 = 01
            Assert.Equal(palette[0], sink.Pixels![y * 256 + 12]); // bits 1-0 = 00
        }
    }
}
