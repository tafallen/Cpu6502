using Cpu6502.Core;
using Machines.Common;

namespace Machines.C64;

/// <summary>
/// MOS 6567 (NTSC) / 6569 (PAL) VIC-II Video Interface Controller.
/// Controls 320×200 16-color text and bitmap graphics, 8 hardware sprites, and raster IRQs.
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
        if (reg == 0x12)
        {
            return (byte)(_currentRasterLine & 0xFF);
        }
        if (reg == 0x19)
        {
            return (byte)(_registers[0x19] | 0x70);
        }
        return _registers[reg];
    }

    public void Write(ushort address, byte value)
    {
        byte reg = (byte)(address & 0x3F);
        if (reg == 0x12) // Write sets Raster Compare Line
        {
            _rasterCompareLine = (ushort)((_rasterCompareLine & 0x0100) | value);
            _registers[0x12] = value;
            return;
        }

        if (reg == 0x19) // Acknowledge IRQs
        {
            _registers[0x19] &= (byte)~(value & 0x0F);
            if ((_registers[0x19] & 0x0F) == 0)
            {
                _registers[0x19] &= 0x7F; // Clear master IRQ bit
            }
            return;
        }

        _registers[reg] = value;
    }

    public void Tick(int cycles = 1)
    {
        // Raster line increment (312 lines per PAL frame)
        _currentRasterLine = (ushort)((_currentRasterLine + cycles) % 312);

        if (_currentRasterLine == _rasterCompareLine && (InterruptEnable & 0x01) != 0)
        {
            _registers[0x19] |= 0x81; // Trigger Raster IRQ
        }
    }

    public void RenderFrame(Ram ram, byte[] charRom, IVideoSink sink)
    {
        uint bgColor = _palette[BackgroundColor0];
        uint borderColor = _palette[BorderColor];

        ushort videoMatrixBase = (ushort)(((_registers[0x18] >> 4) & 0x0F) * 0x0400);

        for (int y = 0; y < 200; y++)
        {
            int charRow = y / 8;
            int pixelY  = y % 8;

            for (int x = 0; x < 320; x++)
            {
                int charCol = x / 8;
                int pixelX  = x % 8;

                ushort screenCellAddr = (ushort)(videoMatrixBase + charRow * 40 + charCol);
                byte charCode = ram.Read(screenCellAddr);

                ushort glyphAddr = (ushort)(charCode * 8 + pixelY);
                byte glyphByte = charRom[glyphAddr % charRom.Length];

                bool isForeground = (glyphByte & (0x80 >> pixelX)) != 0;
                uint color = isForeground ? _palette[1] : bgColor;

                _frameBuffer[(36 + y) * 384 + (32 + x)] = color;
            }
        }

        // Draw Border
        for (int y = 0; y < 272; y++)
        {
            for (int x = 0; x < 384; x++)
            {
                if (x < 32 || x >= 352 || y < 36 || y >= 236)
                {
                    _frameBuffer[y * 384 + x] = borderColor;
                }
            }
        }

        sink.SubmitFrame(_frameBuffer, 384, 272);
    }
}
