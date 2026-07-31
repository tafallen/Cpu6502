using Cpu6502.Core;
using Machines.Common;

namespace Machines.C64;

/// <summary>
/// MOS 6567 (NTSC) / 6569 (PAL) VIC-II Video Interface Controller.
/// High-performance 40×25 cell-based glyph unpacking and Span border renderer.
/// </summary>
public sealed class Vic2Video : IBus
{
    private readonly byte[] _registers = new byte[0x40];
    private readonly uint[] _frameBuffer = new uint[384 * 272];
    private ushort _currentRasterLine;
    private ushort _rasterCompareLine;

    private readonly uint[] _palette = new uint[16]
    {
        0xFF000000, // 0: Black
        0xFFFFFFFF, // 1: White
        0xFF880000, // 2: Red
        0xFFAAEFFF, // 3: Cyan
        0xFFCC44CC, // 4: Purple
        0xFF00CC55, // 5: Green
        0xFF0000AA, // 6: Blue
        0xFFEEEE77, // 7: Yellow
        0xFFDD8855, // 8: Orange
        0xFF664400, // 9: Brown
        0xFFFF7777, // 10: Light Red
        0xFF333333, // 11: Dark Grey
        0xFF777777, // 12: Grey
        0xFFAAFF66, // 13: Light Green
        0xFF0088FF, // 14: Light Blue
        0xFFBBBBBB  // 15: Light Grey
    };

    public byte BorderColor
    {
        get => (byte)(_registers[0x20] & 0x0F);
        set => _registers[0x20] = value;
    }

    public byte BackgroundColor0
    {
        get => (byte)(_registers[0x21] & 0x0F);
        set => _registers[0x21] = value;
    }

    public ushort CurrentRasterLine => _currentRasterLine;

    public byte InterruptStatus
    {
        get => _registers[0x19];
        set => _registers[0x19] = value;
    }

    public byte InterruptEnable
    {
        get => _registers[0x1A];
        set => _registers[0x1A] = value;
    }

    public bool Irq => (InterruptStatus & 0x80) != 0;

    public byte Read(ushort address)
    {
        byte reg = (byte)(address & 0x3F);
        if (reg == 0x12) return (byte)(_currentRasterLine & 0xFF);
        if (reg == 0x19) return (byte)(_registers[0x19] | 0x70);
        return _registers[reg];
    }

    public void Write(ushort address, byte value)
    {
        byte reg = (byte)(address & 0x3F);
        if (reg == 0x12)
        {
            _rasterCompareLine = (ushort)((_rasterCompareLine & 0x0100) | value);
            _registers[0x12] = value;
            return;
        }

        if (reg == 0x19)
        {
            _registers[0x19] &= (byte)~(value & 0x0F);
            if ((_registers[0x19] & 0x0F) == 0)
            {
                _registers[0x19] &= 0x7F;
            }
            return;
        }

        _registers[reg] = value;
    }

    public void Tick(int cycles = 1)
    {
        _currentRasterLine = (ushort)((_currentRasterLine + cycles) % 312);

        if (_currentRasterLine == _rasterCompareLine && (InterruptEnable & 0x01) != 0)
        {
            _registers[0x19] |= 0x81;
        }
    }

    public void RenderFrame(Ram ram, byte[] charRom, IVideoSink sink)
    {
        uint bgColor = _palette[BackgroundColor0];
        uint borderColor = _palette[BorderColor];

        // 1. High-speed Span border clearing (Top 36 lines & Bottom 36 lines)
        Span<uint> bufferSpan = _frameBuffer;
        bufferSpan.Slice(0, 36 * 384).Fill(borderColor);
        bufferSpan.Slice(236 * 384, 36 * 384).Fill(borderColor);

        ushort videoMatrixBase = (ushort)(((_registers[0x18] >> 4) & 0x0F) * 0x0400);

        // 2. 40×25 Cell-Based Glyph Unpacking
        for (int charRow = 0; charRow < 25; charRow++)
        {
            int screenY = 36 + charRow * 8;
            ushort rowCellAddr = (ushort)(videoMatrixBase + charRow * 40);

            for (int charCol = 0; charCol < 40; charCol++)
            {
                int screenX = 32 + charCol * 8;
                byte charCode = ram.Read((ushort)(rowCellAddr + charCol));
                ushort glyphBase = (ushort)(charCode * 8);

                for (int py = 0; py < 8; py++)
                {
                    byte glyphByte = charRom[(glyphBase + py) % charRom.Length];
                    int pixelOffset = (screenY + py) * 384 + screenX;

                    _frameBuffer[pixelOffset + 0] = (glyphByte & 0x80) != 0 ? _palette[1] : bgColor;
                    _frameBuffer[pixelOffset + 1] = (glyphByte & 0x40) != 0 ? _palette[1] : bgColor;
                    _frameBuffer[pixelOffset + 2] = (glyphByte & 0x20) != 0 ? _palette[1] : bgColor;
                    _frameBuffer[pixelOffset + 3] = (glyphByte & 0x10) != 0 ? _palette[1] : bgColor;
                    _frameBuffer[pixelOffset + 4] = (glyphByte & 0x08) != 0 ? _palette[1] : bgColor;
                    _frameBuffer[pixelOffset + 5] = (glyphByte & 0x04) != 0 ? _palette[1] : bgColor;
                    _frameBuffer[pixelOffset + 6] = (glyphByte & 0x02) != 0 ? _palette[1] : bgColor;
                    _frameBuffer[pixelOffset + 7] = (glyphByte & 0x01) != 0 ? _palette[1] : bgColor;
                }
            }

            // Fill left/right margins for row
            for (int py = 0; py < 8; py++)
            {
                int lineOffset = (screenY + py) * 384;
                bufferSpan.Slice(lineOffset, 32).Fill(borderColor);
                bufferSpan.Slice(lineOffset + 352, 32).Fill(borderColor);
            }
        }

        sink.SubmitFrame(_frameBuffer, 384, 272);
    }
}
