using Machines.Common;
using Machines.Vic20;

namespace Machines.Vic20.Tests;

public class VicITests
{
    // ── register offsets (relative to chip base, i.e. address & 0xF) ─────────
    private const ushort REG_ORIGIN_X  = 0;  // $9000: horizontal origin (2-px units)
    private const ushort REG_ORIGIN_Y  = 1;  // $9001: vertical origin (lines)
    private const ushort REG_COLUMNS   = 2;  // $9002: columns[6:0] + screenBase bit9[7]
    private const ushort REG_ROWS      = 3;  // $9003: rows[6:1] + charHeight[0]
    private const ushort REG_RASTER    = 4;  // $9004: raster counter (read-only in hw)
    private const ushort REG_ADDRS     = 5;  // $9005: screenBase[13:10] | charBase[13:10]
    private const ushort REG_BASS      = 10; // $900A: bass freq[7:1] + on[0]
    private const ushort REG_ALTO      = 11; // $900B: alto freq[7:1] + on[0]
    private const ushort REG_SOPRANO   = 12; // $900C: soprano freq[7:1] + on[0]
    private const ushort REG_NOISE     = 13; // $900D: noise freq[7:1] + on[0]
    private const ushort REG_VOLUME    = 14; // $900E: aux color[7:4] + master volume[3:0]
    private const ushort REG_COLORS    = 15; // $900F: background[7:4] + border[3:1] + reverse[0]

    // ── helpers ───────────────────────────────────────────────────────────────

    private static VicI Make(IAudioSink? audio = null) => new(audio);

    // Set up the layout registers from human-readable values.
    // screenBase and charBase must be 512-byte aligned (multiples of 0x200).
    private static void ConfigureLayout(
        VicI vic,
        ushort screenBase, ushort charBase,
        int cols, int rows,
        int originX = 0, int originY = 0)
    {
        // screenBase bits 13-10 → REG_ADDRS bits 7-4
        // screenBase bit 9     → REG_COLUMNS bit 7
        byte addrScreenBits = (byte)((screenBase >> 10) & 0x0F);
        byte addrCharBits   = (byte)((charBase   >> 10) & 0x0F);
        byte colBit9        = (byte)((screenBase >>  9) & 0x01);

        vic.Write(REG_ORIGIN_X, (byte)(originX & 0x7F));
        vic.Write(REG_ORIGIN_Y, (byte)originY);
        vic.Write(REG_COLUMNS,  (byte)((colBit9 << 7) | (cols & 0x7F)));
        vic.Write(REG_ROWS,     (byte)((rows & 0x3F) << 1));
        vic.Write(REG_ADDRS,    (byte)((addrScreenBits << 4) | addrCharBits));
    }

    // Build a VIC memory array (16 KB) with screen codes and char bitmaps placed
    // at the correct offsets, ready to hand to VicI.ReadVicMemory.
    private static byte[] BuildVicMem(
        ushort screenBase, byte[] screenCodes,
        ushort charBase,   byte[] charBitmaps)
    {
        var mem = new byte[16384];
        screenCodes.CopyTo(mem, screenBase);
        charBitmaps.CopyTo(mem, charBase);
        return mem;
    }

    private sealed class CaptureSink : IVideoSink
    {
        public uint[]? Pixels;
        public int Width, Height;
        public void SubmitFrame(ReadOnlySpan<uint> pixels, int width, int height)
        {
            Pixels = pixels.ToArray();
            Width  = width;
            Height = height;
        }
    }

    private sealed class CaptureAudio : IAudioSink
    {
        public short[]? Samples;
        public int SampleRate;
        public void SubmitSamples(ReadOnlySpan<short> samples, int sampleRate)
        {
            Samples    = samples.ToArray();
            SampleRate = sampleRate;
        }
    }

    // ── register access ───────────────────────────────────────────────────────

    [Fact]
    public void AllRegisters_WriteRead_RoundTrip()
    {
        var vic = Make();
        for (ushort i = 0; i < 16; i++)
        {
            vic.Write(i, (byte)(0xA0 | i));
            Assert.Equal((byte)(0xA0 | i), vic.Read(i));
        }
    }

    // ── default register state ────────────────────────────────────────────────

    [Fact]
    public void DefaultColumns_Is22()
    {
        Assert.Equal(22, Make().Columns);
    }

    [Fact]
    public void DefaultRows_Is23()
    {
        Assert.Equal(23, Make().Rows);
    }

    // ── frame dimensions ──────────────────────────────────────────────────────

    [Fact]
    public void RenderFrame_ProducesCorrectDimensions()
    {
        var sink = new CaptureSink();
        Make().RenderFrame(sink);
        Assert.Equal(VicI.FrameWidth,  sink.Width);
        Assert.Equal(VicI.FrameHeight, sink.Height);
        Assert.Equal(VicI.FrameWidth * VicI.FrameHeight, sink.Pixels!.Length);
    }

    // ── border colour ─────────────────────────────────────────────────────────

    [Fact]
    public void BorderColor_FillsPixelsOutsideActiveArea()
    {
        // 1 col × 1 row, origin at (8, 8): active area = 8×8 at pixel (16, 8)
        // (originX is in 2-px units, so originX=8 → pixel x=16)
        // Pixel (0, 0) is in the border.
        ushort screenBase = 0x0200, charBase = 0x0000;
        var mem = BuildVicMem(screenBase, [0x00], charBase, new byte[8]);
        var vic = Make();
        ConfigureLayout(vic, screenBase, charBase, cols: 1, rows: 1, originX: 8, originY: 8);
        vic.ReadVicMemory = a => mem[a & 0x3FFF];
        vic.ReadColorRam  = _ => 1; // white

        // border color = color 2 (red) via REG_COLORS bits 3-1
        vic.Write(REG_COLORS, (byte)(0 << 4 | 2 << 1)); // bg=0(black), border=2(red)

        var sink = new CaptureSink();
        vic.RenderFrame(sink);

        uint borderColor = VicI.Palette[2];
        Assert.Equal(borderColor, sink.Pixels![0]); // pixel (0,0) is in the border
    }

    // ── background colour ─────────────────────────────────────────────────────

    [Fact]
    public void BackgroundColor_FillsZeroBitsInCharCell()
    {
        // char 0 with all-zero bitmap → every pixel should be background
        ushort screenBase = 0x0200, charBase = 0x0000;
        var charBitmap = new byte[8]; // all zero
        var mem = BuildVicMem(screenBase, [0x00], charBase, charBitmap);

        var vic = Make();
        ConfigureLayout(vic, screenBase, charBase, cols: 1, rows: 1);
        vic.ReadVicMemory = a => mem[a & 0x3FFF];
        vic.ReadColorRam  = _ => 1;

        // background = color 3 (cyan)
        vic.Write(REG_COLORS, (byte)(3 << 4));

        var sink = new CaptureSink();
        vic.RenderFrame(sink);

        uint bgColor = VicI.Palette[3];
        // Active area starts at (0*2, 0) = (0,0); char cell is pixels 0-7 x 0-7
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                Assert.Equal(bgColor, sink.Pixels![y * VicI.FrameWidth + x]);
    }

    // ── foreground colour ─────────────────────────────────────────────────────

    [Fact]
    public void ForegroundColor_FillsOneBitsInCharCell()
    {
        // char 0 with all-ones bitmap → every pixel should be foreground
        ushort screenBase = 0x0200, charBase = 0x0000;
        var charBitmap = Enumerable.Repeat((byte)0xFF, 8).ToArray(); // all set
        var mem = BuildVicMem(screenBase, [0x00], charBase, charBitmap);

        var vic = Make();
        ConfigureLayout(vic, screenBase, charBase, cols: 1, rows: 1);
        vic.ReadVicMemory = a => mem[a & 0x3FFF];

        byte fgColor = 1; // white
        vic.ReadColorRam = _ => fgColor;
        vic.Write(REG_COLORS, 0x00); // background = black

        var sink = new CaptureSink();
        vic.RenderFrame(sink);

        uint expectedFg = VicI.Palette[fgColor];
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                Assert.Equal(expectedFg, sink.Pixels![y * VicI.FrameWidth + x]);
    }

    // ── per-bit rendering ─────────────────────────────────────────────────────

    [Fact]
    public void CharBitmap_0xAA_AlternatesPixels()
    {
        // 0xAA = 1010_1010 MSB-first → pixels 0,2,4,6 = foreground, 1,3,5,7 = background
        ushort screenBase = 0x0200, charBase = 0x0000;
        var charBitmap = Enumerable.Repeat((byte)0xAA, 8).ToArray();
        var mem = BuildVicMem(screenBase, [0x00], charBase, charBitmap);

        var vic = Make();
        ConfigureLayout(vic, screenBase, charBase, cols: 1, rows: 1);
        vic.ReadVicMemory = a => mem[a & 0x3FFF];
        vic.ReadColorRam  = _ => 1; // white fg
        vic.Write(REG_COLORS, 0x00); // black bg

        var sink = new CaptureSink();
        vic.RenderFrame(sink);

        uint fg = VicI.Palette[1]; // white
        uint bg = VicI.Palette[0]; // black

        // Row 0, check all 8 pixels
        for (int x = 0; x < 8; x++)
        {
            uint expected = (x % 2 == 0) ? fg : bg; // bit 7 first = MSB = even pixels
            Assert.Equal(expected, sink.Pixels![x]);
        }
    }

    // ── character code indexing ───────────────────────────────────────────────

    [Fact]
    public void CharCode_SelectsCorrectBitmapSlot()
    {
        // Screen code 2 should use the bitmap at charBase + 2*8 = charBase + 16
        ushort screenBase = 0x0200, charBase = 0x0000;
        var mem = new byte[16384];
        mem[screenBase] = 0x02; // screen code 2
        // char 2 bitmap: first row = 0xFF, rest = 0x00
        mem[charBase + 2 * 8 + 0] = 0xFF;

        var vic = Make();
        ConfigureLayout(vic, screenBase, charBase, cols: 1, rows: 1);
        vic.ReadVicMemory = a => mem[a & 0x3FFF];
        vic.ReadColorRam  = _ => 1; // white
        vic.Write(REG_COLORS, 0x00); // black bg

        var sink = new CaptureSink();
        vic.RenderFrame(sink);

        uint fg = VicI.Palette[1];
        uint bg = VicI.Palette[0];

        // Row 0 = all foreground (0xFF row)
        for (int x = 0; x < 8; x++)
            Assert.Equal(fg, sink.Pixels![x]);
        // Row 1 = all background (0x00 row)
        for (int x = 0; x < 8; x++)
            Assert.Equal(bg, sink.Pixels![VicI.FrameWidth + x]);
    }

    // ── multi-character layout ────────────────────────────────────────────────

    [Fact]
    public void TwoColumns_SecondCharAtCorrectPixelOffset()
    {
        // col 0 = char 0 (blank), col 1 = char 1 (solid)
        ushort screenBase = 0x0200, charBase = 0x0000;
        var mem = new byte[16384];
        mem[screenBase + 0] = 0x00; // col 0: blank char
        mem[screenBase + 1] = 0x01; // col 1: solid char
        for (int r = 0; r < 8; r++) mem[charBase + 0 * 8 + r] = 0x00; // char 0: blank
        for (int r = 0; r < 8; r++) mem[charBase + 1 * 8 + r] = 0xFF; // char 1: solid

        var vic = Make();
        ConfigureLayout(vic, screenBase, charBase, cols: 2, rows: 1);
        vic.ReadVicMemory = a => mem[a & 0x3FFF];
        vic.ReadColorRam  = _ => 1;
        vic.Write(REG_COLORS, 0x00);

        var sink = new CaptureSink();
        vic.RenderFrame(sink);

        uint fg = VicI.Palette[1];
        uint bg = VicI.Palette[0];

        // col 0 pixels (x=0..7, y=0) → background
        for (int x = 0; x < 8; x++)
            Assert.Equal(bg, sink.Pixels![x]);
        // col 1 pixels (x=8..15, y=0) → foreground
        for (int x = 8; x < 16; x++)
            Assert.Equal(fg, sink.Pixels![x]);
    }

    // ── per-cell colour from colour RAM ──────────────────────────────────────

    [Fact]
    public void ColorRam_PerCellForeground()
    {
        // Two cells side by side: left = color 1 (white), right = color 2 (red)
        ushort screenBase = 0x0200, charBase = 0x0000;
        var mem = new byte[16384];
        mem[screenBase + 0] = 0x01; // both cells use char 1 (solid)
        mem[screenBase + 1] = 0x01;
        for (int r = 0; r < 8; r++) mem[charBase + 1 * 8 + r] = 0xFF;

        var vic = Make();
        ConfigureLayout(vic, screenBase, charBase, cols: 2, rows: 1);
        vic.ReadVicMemory = a => mem[a & 0x3FFF];
        vic.ReadColorRam  = idx => idx == 0 ? (byte)1 : (byte)2; // cell 0=white, cell 1=red
        vic.Write(REG_COLORS, 0x00);

        var sink = new CaptureSink();
        vic.RenderFrame(sink);

        Assert.Equal(VicI.Palette[1], sink.Pixels![0]);  // left cell foreground = white
        Assert.Equal(VicI.Palette[2], sink.Pixels![8]);  // right cell foreground = red
    }

    // ── reverse mode ─────────────────────────────────────────────────────────

    [Fact]
    public void ReverseMode_SwapsForegroundAndBackground()
    {
        ushort screenBase = 0x0200, charBase = 0x0000;
        // char 0: first pixel = 1 (foreground bit)
        var mem = new byte[16384];
        mem[screenBase] = 0x00;
        mem[charBase]   = 0x80; // bit 7 = 1 → pixel 0 = foreground in normal mode

        var vic = Make();
        ConfigureLayout(vic, screenBase, charBase, cols: 1, rows: 1);
        vic.ReadVicMemory = a => mem[a & 0x3FFF];
        vic.ReadColorRam  = _ => 1; // white fg
        vic.Write(REG_COLORS, (byte)((3 << 4) | 1)); // bg=cyan, reverse=1

        var sink = new CaptureSink();
        vic.RenderFrame(sink);

        // In reverse mode, a set bit renders background colour instead of foreground
        Assert.Equal(VicI.Palette[3], sink.Pixels![0]); // pixel 0 = bg (cyan), not fg (white)
    }

    // ── palette ───────────────────────────────────────────────────────────────

    [Fact]
    public void Palette_Has16Entries_FirstIsBlack_SecondIsWhite()
    {
        Assert.Equal(16, VicI.Palette.Length);
        Assert.Equal(0xFF000000u, VicI.Palette[0]); // black
        Assert.Equal(0xFFFFFFFFu, VicI.Palette[1]); // white
    }

    // ── audio ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Audio_SubmitsSamplesPerFrame_AtCorrectRate()
    {
        var audio = new CaptureAudio();
        var vic = Make(audio);
        vic.RenderFrame(new CaptureSink());
        Assert.NotNull(audio.Samples);
        Assert.Equal(VicI.SamplesPerFrame, audio.Samples!.Length);
        Assert.Equal(VicI.SampleRate, audio.SampleRate);
    }

    [Fact]
    public void Audio_ZeroVolume_AllSamplesAreSilent()
    {
        var audio = new CaptureAudio();
        var vic = Make(audio);
        vic.Write(REG_BASS,    0x01); // oscillator on, any frequency
        vic.Write(REG_VOLUME,  0x00); // master volume = 0
        vic.RenderFrame(new CaptureSink());
        Assert.All(audio.Samples!, s => Assert.Equal(0, s));
    }

    [Fact]
    public void Audio_OscillatorOnWithVolume_ProducesNonSilentOutput()
    {
        var audio = new CaptureAudio();
        var vic = Make(audio);
        vic.Write(REG_BASS,   0x81); // mid-range frequency, on
        vic.Write(REG_VOLUME, 0x0F); // max volume
        vic.RenderFrame(new CaptureSink());
        Assert.Contains(audio.Samples!, s => s != 0);
    }

    [Fact]
    public void Audio_OscillatorOff_AllSamplesAreSilent()
    {
        var audio = new CaptureAudio();
        var vic = Make(audio);
        vic.Write(REG_BASS,   0x80); // frequency set but oscillator OFF (bit 0 = 0)
        vic.Write(REG_VOLUME, 0x0F);
        vic.RenderFrame(new CaptureSink());
        Assert.All(audio.Samples!, s => Assert.Equal(0, s));
    }

    [Fact]
    public void Audio_NoSink_DoesNotThrow()
    {
        var vic = Make(audio: null);
        var ex = Record.Exception(() => vic.RenderFrame(new CaptureSink()));
        Assert.Null(ex);
    }
}
