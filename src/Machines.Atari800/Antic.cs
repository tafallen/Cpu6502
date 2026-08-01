using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cpu6502.Core;
using Machines.Common;

namespace Machines.Atari800;

/// <summary>
/// Atari ANTIC (AlphaNumeric Television Interface Controller) video DMA coprocessor ($D400–$D40F).
/// Ultra-high performance 336×240 character/graphics renderer.
/// </summary>
public sealed class Antic : IBus
{
    private readonly byte[] _registers = new byte[0x10];
    private readonly uint[] _frameBuffer = new uint[336 * 240];
    private ushort _dlistAddress;
    private ushort _vcount;

    public ushort DlistAddress => _dlistAddress;
    public byte NmiStatus { get; set; }
    public byte NmiEnable { get; set; }

    public bool Nmi => (NmiStatus & NmiEnable & 0xC0) != 0;

    public byte Read(ushort address)
    {
        byte reg = (byte)(address & 0x0F);
        if (reg == 0x0B) return (byte)(_vcount >> 1); // VCOUNT
        if (reg == 0x0F) return NmiStatus; // NMIST
        return _registers[reg];
    }

    public void Write(ushort address, byte value)
    {
        byte reg = (byte)(address & 0x0F);
        _registers[reg] = value;

        if (reg == 0x02) _dlistAddress = (ushort)((_dlistAddress & 0xFF00) | value);
        else if (reg == 0x03) _dlistAddress = (ushort)((_dlistAddress & 0x00FF) | (value << 8));
        else if (reg == 0x0E) NmiEnable = value;
        else if (reg == 0x0F) NmiStatus &= 0x1F;
    }

    public void Tick(int cycles = 1)
    {
        _vcount = (ushort)((_vcount + cycles) % 262);

        if (_vcount == 248 && (NmiEnable & 0x40) != 0)
        {
            NmiStatus |= 0x40;
        }
    }

    public void RenderFrame(Ram ram, Gtia gtia, IVideoSink sink)
    {
        byte[]? ramBuf = ram?.DirectWriteBuffer;
        if (ramBuf is null) return;

        Span<uint> bufferSpan = _frameBuffer;
        uint bgColor = 0xFF000000;
        uint fgColor = 0xFFFFFFFF;
        uint bgGlyph = 0xFF222222;
        bufferSpan.Fill(bgColor);

        ushort screenMem = 0x4000;

        ref byte ramRef = ref MemoryMarshal.GetArrayDataReference(ramBuf);
        ref uint fbRef = ref MemoryMarshal.GetArrayDataReference(_frameBuffer);

        for (int row = 0; row < 24; row++)
        {
            int screenY = 24 + row * 8;
            int rowAddr = screenMem + row * 40;

            for (int col = 0; col < 40; col++)
            {
                int screenX = 8 + col * 8;
                byte charCode = Unsafe.Add(ref ramRef, rowAddr + col);

                for (int py = 0; py < 8; py++)
                {
                    int pixelOffset = (screenY + py) * 336 + screenX;

                    Unsafe.Add(ref fbRef, pixelOffset + 0) = (charCode & 0x80) != 0 ? fgColor : bgGlyph;
                    Unsafe.Add(ref fbRef, pixelOffset + 1) = (charCode & 0x40) != 0 ? fgColor : bgGlyph;
                    Unsafe.Add(ref fbRef, pixelOffset + 2) = (charCode & 0x20) != 0 ? fgColor : bgGlyph;
                    Unsafe.Add(ref fbRef, pixelOffset + 3) = (charCode & 0x10) != 0 ? fgColor : bgGlyph;
                    Unsafe.Add(ref fbRef, pixelOffset + 4) = (charCode & 0x08) != 0 ? fgColor : bgGlyph;
                    Unsafe.Add(ref fbRef, pixelOffset + 5) = (charCode & 0x04) != 0 ? fgColor : bgGlyph;
                    Unsafe.Add(ref fbRef, pixelOffset + 6) = (charCode & 0x02) != 0 ? fgColor : bgGlyph;
                    Unsafe.Add(ref fbRef, pixelOffset + 7) = (charCode & 0x01) != 0 ? fgColor : bgGlyph;
                }
            }
        }

        sink.SubmitFrame(_frameBuffer, 336, 240);
    }
}
