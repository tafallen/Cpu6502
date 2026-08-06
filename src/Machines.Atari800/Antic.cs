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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ushort address)
    {
        byte reg = (byte)(address & 0x0F);
        if (reg == 0x0B) return (byte)(_vcount >> 1); // VCOUNT
        if (reg == 0x0F) return NmiStatus; // NMIST
        return _registers[reg];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort address, byte value)
    {
        byte reg = (byte)(address & 0x0F);
        _registers[reg] = value;

        if (reg == 0x02) _dlistAddress = (ushort)((_dlistAddress & 0xFF00) | value);
        else if (reg == 0x03) _dlistAddress = (ushort)((_dlistAddress & 0x00FF) | (value << 8));
        else if (reg == 0x0E) NmiEnable = value;
        else if (reg == 0x0F) NmiStatus &= 0x1F;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Tick(int cycles = 1)
    {
        _vcount += (ushort)cycles;
        if (_vcount >= 262) _vcount -= 262;

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

        // Fill only border regions, not the 320×192 active area that will be overwritten
        bufferSpan.Slice(0, 24 * 336).Fill(bgColor);           // Top border (rows 0-23)
        bufferSpan.Slice(216 * 336, 24 * 336).Fill(bgColor);   // Bottom border (rows 216-239)
        for (int y = 24; y < 216; y++)
        {
            int rowBase = y * 336;
            bufferSpan.Slice(rowBase, 8).Fill(bgColor);         // Left border
            bufferSpan.Slice(rowBase + 328, 8).Fill(bgColor);   // Right border
        }

        ushort screenMem = 0x4000;

        ref byte ramRef = ref MemoryMarshal.GetArrayDataReference(ramBuf);
        ref uint fbRef = ref MemoryMarshal.GetArrayDataReference(_frameBuffer);

        // Branchless color lookup: index 0 = bg, index 1 = fg
        ReadOnlySpan<uint> colors = stackalloc uint[] { bgGlyph, fgColor };

        for (int row = 0; row < 24; row++)
        {
            int rowAddr = screenMem + row * 40;
            int pixelBase = (24 + row * 8) * 336 + 8; // screenY * 336 + screenX(col=0)

            for (int col = 0; col < 40; col++)
            {
                byte charCode = Unsafe.Add(ref ramRef, rowAddr + col);
                int pixelOffset = pixelBase + col * 8;

                for (int py = 0; py < 8; py++)
                {
                    Unsafe.Add(ref fbRef, pixelOffset + 0) = colors[(charCode >> 7) & 1];
                    Unsafe.Add(ref fbRef, pixelOffset + 1) = colors[(charCode >> 6) & 1];
                    Unsafe.Add(ref fbRef, pixelOffset + 2) = colors[(charCode >> 5) & 1];
                    Unsafe.Add(ref fbRef, pixelOffset + 3) = colors[(charCode >> 4) & 1];
                    Unsafe.Add(ref fbRef, pixelOffset + 4) = colors[(charCode >> 3) & 1];
                    Unsafe.Add(ref fbRef, pixelOffset + 5) = colors[(charCode >> 2) & 1];
                    Unsafe.Add(ref fbRef, pixelOffset + 6) = colors[(charCode >> 1) & 1];
                    Unsafe.Add(ref fbRef, pixelOffset + 7) = colors[charCode & 1];

                    pixelOffset += 336; // Advance to next scanline
                }
            }
        }

        sink.SubmitFrame(_frameBuffer, 336, 240);
    }
}
