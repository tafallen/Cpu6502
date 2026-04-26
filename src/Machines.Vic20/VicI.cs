using Cpu6502.Core;
using Machines.Common;

namespace Machines.Vic20;

/// <summary>
/// MOS 6560/6561 VIC-I chip — video display and sound for the VIC-20.
///
/// Registers ($9000–$900F, 16 bytes):
///   $9000  Horizontal origin (2-pixel units, bits 6-0)
///   $9001  Vertical origin (lines)
///   $9002  Columns (bits 6-0) + screen base bit 9 (bit 7)
///   $9003  Rows (bits 6-1) + character height (bit 0)
///   $9004  Raster counter (read-only)
///   $9005  Screen base bits 13-10 (bits 7-4) + char base bits 13-10 (bits 3-0)
///   $900A  Bass oscillator frequency (bits 7-1) + on/off (bit 0)
///   $900B  Alto oscillator frequency + on/off
///   $900C  Soprano oscillator frequency + on/off
///   $900D  Noise oscillator frequency + on/off
///   $900E  Auxiliary colour (bits 7-4) + master volume (bits 3-0)
///   $900F  Background colour (bits 7-4) + border colour (bits 3-1) + reverse (bit 0)
///
/// Memory access:
///   ReadVicMemory(vicAddr) — 14-bit VIC address space (16 KB). The machine wires this
///   to translate VIC addresses to CPU bus reads.
///   ReadColorRam(cellIndex) — returns the 4-bit foreground colour for a cell.
///
/// Output: FrameWidth × FrameHeight ARGB32 pixels submitted to IVideoSink each frame.
/// Audio:  SamplesPerFrame mono 16-bit PCM samples at SampleRate Hz submitted to IAudioSink.
/// </summary>
public sealed class VicI : IBus
{
    // ── public constants ──────────────────────────────────────────────────────

    public const int FrameWidth      = 256;
    public const int FrameHeight     = 272;
    public const int SampleRate      = 44100;
    public const int FrameRate       = 50;     // PAL
    public const int SamplesPerFrame = SampleRate / FrameRate; // 882

    // PAL tone base clock: VIC dot clock / 128 ≈ 8659 Hz
    private const double ToneBaseClock = 1_108_405.0 / 128.0;

    // ── VIC-20 colour palette (ARGB32) ────────────────────────────────────────

    public static readonly uint[] Palette =
    [
        0xFF000000, // 0  Black
        0xFFFFFFFF, // 1  White
        0xFF993333, // 2  Red
        0xFF88CCCC, // 3  Cyan
        0xFFCC55CC, // 4  Magenta/Purple
        0xFF55AA55, // 5  Green
        0xFF3333BB, // 6  Blue
        0xFFCCCC55, // 7  Yellow
        0xFFDD8855, // 8  Orange
        0xFFDDAA88, // 9  Light orange
        0xFFEEAAAA, // 10 Light red/Pink
        0xFFAADDDD, // 11 Light cyan
        0xFFEE88EE, // 12 Light purple
        0xFFAADDAA, // 13 Light green
        0xFF8888EE, // 14 Light blue
        0xFFEEEEAA, // 15 Light yellow
    ];

    // ── registers ─────────────────────────────────────────────────────────────

    private readonly byte[] _regs = new byte[16];

    // ── memory callbacks ──────────────────────────────────────────────────────

    /// <summary>Read a byte from the 14-bit VIC address space (0x0000–0x3FFF).</summary>
    public Func<ushort, byte> ReadVicMemory = _ => 0x00;

    /// <summary>Read the 4-bit foreground colour for a given cell index.</summary>
    public Func<ushort, byte> ReadColorRam = _ => 0x01; // default: white

    // ── audio ─────────────────────────────────────────────────────────────────

    private readonly IAudioSink? _audio;
    private readonly short[]     _audioBuffer = new short[SamplesPerFrame];

    // Per-oscillator phase accumulators (0.0–1.0)
    private double _phaseA, _phaseB, _phaseC;
    private uint   _noiseLfsr = 0x7FFF; // 15-bit LFSR

    // ── frame pixel buffer ────────────────────────────────────────────────────

    private readonly uint[] _pixels = new uint[FrameWidth * FrameHeight];

    // ── IBus ──────────────────────────────────────────────────────────────────

    public VicI(IAudioSink? audio = null)
    {
        _audio = audio;
        InitDefaults();
    }

    public byte Read(ushort address)  => _regs[address & 0xF];
    public void Write(ushort address, byte value) => _regs[address & 0xF] = value;

    // ── register-derived properties ───────────────────────────────────────────

    public int Columns => _regs[2] & 0x7F;
    public int Rows    => (_regs[3] & 0x7E) >> 1;

    private int    OriginX     => (_regs[0] & 0x7F) * 2;    // 2-pixel units
    private int    OriginY     => _regs[1];
    private ushort ScreenBase  => (ushort)(((_regs[5] >> 4) << 10) | ((_regs[2] >> 7) << 9));
    private ushort CharBase    => (ushort)((_regs[5] & 0x0F) << 10);
    private int    MasterVol   => _regs[14] & 0x0F;
    private uint   BgColor     => Palette[(_regs[15] >> 4) & 0x0F];
    private uint   BorderColor => Palette[(_regs[15] >> 1) & 0x07];
    private bool   Reverse     => (_regs[15] & 0x01) != 0;

    // ── rendering ─────────────────────────────────────────────────────────────

    public void RenderFrame(IVideoSink sink)
    {
        RenderPixels();
        GenerateAudio();
        sink.SubmitFrame(_pixels, FrameWidth, FrameHeight);
    }

    private void RenderPixels()
    {
        int ox      = OriginX;
        int oy      = OriginY;
        int cols    = Columns;
        int rows    = Rows;
        int areaW   = cols * 8;
        int areaH   = rows * 8;
        int border  = (int)BorderColor;

        // Fill entire frame with border colour first
        Array.Fill(_pixels, BorderColor);

        // Render active character area
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int cellIdx    = row * cols + col;
                byte code      = ReadVicMemory((ushort)((ScreenBase + cellIdx) & 0x3FFF));
                byte fgIdx     = (byte)(ReadColorRam((ushort)cellIdx) & 0x0F);
                uint fg        = Palette[fgIdx];
                uint bg        = BgColor;
                if (Reverse) (fg, bg) = (bg, fg);

                int cellX = ox + col * 8;
                int cellY = oy + row * 8;

                for (int py = 0; py < 8; py++)
                {
                    int screenY = cellY + py;
                    if (screenY < 0 || screenY >= FrameHeight) continue;

                    byte bitmap = ReadVicMemory((ushort)((CharBase + code * 8 + py) & 0x3FFF));

                    for (int px = 0; px < 8; px++)
                    {
                        int screenX = cellX + px;
                        if (screenX < 0 || screenX >= FrameWidth) continue;

                        bool set = (bitmap & (0x80 >> px)) != 0;
                        _pixels[screenY * FrameWidth + screenX] = set ? fg : bg;
                    }
                }
            }
        }
    }

    // ── audio synthesis ───────────────────────────────────────────────────────

    private void GenerateAudio()
    {
        if (_audio is null) return;

        int vol = MasterVol;
        if (vol == 0)
        {
            Array.Clear(_audioBuffer);
            _audio.SubmitSamples(_audioBuffer, SampleRate);
            return;
        }

        short amplitude = (short)(vol * 2184); // 2184 ≈ 32767/15

        for (int i = 0; i < SamplesPerFrame; i++)
        {
            int sample = 0;
            sample += OscillatorSample(10, ref _phaseA, amplitude); // bass
            sample += OscillatorSample(11, ref _phaseB, amplitude); // alto
            sample += OscillatorSample(12, ref _phaseC, amplitude); // soprano
            sample += NoiseSample(13, amplitude);
            _audioBuffer[i] = (short)Math.Clamp(sample, short.MinValue, short.MaxValue);
        }

        _audio.SubmitSamples(_audioBuffer, SampleRate);
    }

    private int OscillatorSample(int reg, ref double phase, short amplitude)
    {
        byte r = _regs[reg];
        if ((r & 0x01) == 0) return 0; // oscillator off

        double freq = ToneBaseClock / (256 - (r & 0xFE));
        phase += freq / SampleRate;
        if (phase >= 1.0) phase -= 1.0;
        return phase < 0.5 ? amplitude : -amplitude;
    }

    private int NoiseSample(int reg, short amplitude)
    {
        byte r = _regs[reg];
        if ((r & 0x01) == 0) return 0;

        // 15-bit Galois LFSR (feedback polynomial x^15 + x^14 + 1)
        _noiseLfsr ^= (uint)((_noiseLfsr & 1) * 0x6000);
        _noiseLfsr >>= 1;
        return (_noiseLfsr & 1) == 1 ? amplitude : -amplitude;
    }

    // ── default register values (unexpanded PAL VIC-20) ──────────────────────

    private void InitDefaults()
    {
        _regs[0]  = 0x0C; // horizontal origin = 12 (24 pixels)
        _regs[1]  = 0x26; // vertical origin = 38
        _regs[2]  = 0x96; // 22 columns, screen base bit9 = 1
        _regs[3]  = 0x2E; // 23 rows, 8-pixel chars
        _regs[5]  = 0xF0; // screen base bits 13-10 = 0xF, char base = 0
        _regs[14] = 0x00; // volume = 0
        _regs[15] = 0x1B; // bg = 1 (white), border = 5 (green) — standard VIC-20 startup
    }
}
