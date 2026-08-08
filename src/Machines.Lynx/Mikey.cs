using System.Runtime.CompilerServices;
using Cpu6502.Core;
using Machines.Common;

namespace Machines.Lynx;

/// <summary>
/// Atari Lynx MIKEY custom VLSI chip ($FD00–$FDFF).
/// Handles LCD video timing, 16-color palette (4,096 RGB444 color space),
/// 8 16-bit countdown timers, 4-channel audio synthesizer, and interrupt controller.
/// </summary>
public sealed class Mikey : IBus
{
    private readonly byte[] _registers = new byte[0x100];
    private readonly uint[] _palette = new uint[16];
    private readonly uint[] _frameBuffer = new uint[160 * 102];

    public byte IrqStatus { get; private set; }
    public byte IrqEnable { get; private set; }
    public bool Irq => (IrqStatus & IrqEnable) != 0;

    private readonly IAudioSink? _audio;
    private readonly Cartridge? _cartridge;

    public Mikey(IAudioSink? audio = null, Cartridge? cartridge = null)
    {
        _audio = audio;
        _cartridge = cartridge;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ushort address)
    {
        byte reg = (byte)(address & 0xFF);
        return reg switch
        {
            0x80 => IrqStatus,
            0x81 => IrqEnable,
            0x88 => 0x01, // MIKEYHREV
            _ => _registers[reg]
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort address, byte value)
    {
        byte reg = (byte)(address & 0xFF);
        _registers[reg] = value;

        // Palette Green/Blue & Red registers ($FDA0–$FDBF)
        if (reg >= 0xA0 && reg <= 0xBF)
        {
            int colorIdx = (reg - 0xA0) & 0x0F;
            if ((reg & 0x10) == 0) // Green/Blue ($FDA0–$FDAF)
            {
                byte g = (byte)((value >> 4) * 17);
                byte b = (byte)((value & 0x0F) * 17);
                uint r = (_palette[colorIdx] >> 16) & 0xFF;
                _palette[colorIdx] = 0xFF000000u | (r << 16) | ((uint)g << 8) | b;
            }
            else // Red ($FDB0–$FDBF)
            {
                byte r = (byte)((value & 0x0F) * 17);
                _palette[colorIdx] = (_palette[colorIdx] & 0xFF00FFFFu) | ((uint)r << 16);
            }
        }
        else if (reg == 0x80) // Clear IRQ flags
        {
            IrqStatus &= (byte)~value;
        }
        else if (reg == 0x81) // Set IRQ Enable mask
        {
            IrqEnable = value;
        }
        else if (reg == 0x87) // SYSCTL1
        {
            // Cartridge Address Strobe (Bit 0). Data comes from IODAT (0x8B) Bit 1.
            bool strobe = (value & 0x01) != 0;
            bool data = (_registers[0x8B] & 0x02) != 0;
            _cartridge?.SetStrobe(strobe, data);
        }
    }

    private int _vblankAccum;
    private int _hblankAccum;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Tick(int cycles = 1)
    {
        _hblankAccum += cycles;
        if (_hblankAccum >= 500)
        {
            _hblankAccum -= 500;
            IrqStatus |= 0x01; // Timer 0 (HBLANK)
        }

        _vblankAccum += cycles;
        if (_vblankAccum >= 80000)
        {
            _vblankAccum -= 80000;
            IrqStatus |= 0x04; // Timer 2 (VBLANK)
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RenderFrame(Ram ram, IVideoSink sink)
    {
        byte[]? ramBuf = ram.DirectWriteBuffer;
        if (ramBuf is null) return;

        // LCD display base address from DISPADDR ($FD94/$FD95)
        ushort dispAddr = (ushort)(_registers[0x94] | (_registers[0x95] << 8));
        if (dispAddr == 0 || dispAddr + (160 * 102 / 2) > ramBuf.Length)
        {
            dispAddr = 0xC000; // Default Lynx screen base
        }

        Span<uint> bufferSpan = _frameBuffer;
        ReadOnlySpan<uint> paletteSpan = _palette;

        // 160×102 4-bit packed pixels (2 pixels per byte)
        for (int y = 0; y < 102; y++)
        {
            int lineBase = dispAddr + (y * 80);
            int pixelOffset = y * 160;

            if (lineBase + 80 <= ramBuf.Length)
            {
                // Unroll loop 4x: process 4 bytes (8 pixels) per iteration
                for (int x = 0; x < 160; x += 8)
                {
                    int srcIdx = lineBase + (x >> 1);
                    int dstIdx = pixelOffset + x;

                    byte b0 = ramBuf[srcIdx];
                    byte b1 = ramBuf[srcIdx + 1];
                    byte b2 = ramBuf[srcIdx + 2];
                    byte b3 = ramBuf[srcIdx + 3];

                    bufferSpan[dstIdx]     = paletteSpan[b0 >> 4];
                    bufferSpan[dstIdx + 1] = paletteSpan[b0 & 0x0F];
                    bufferSpan[dstIdx + 2] = paletteSpan[b1 >> 4];
                    bufferSpan[dstIdx + 3] = paletteSpan[b1 & 0x0F];
                    bufferSpan[dstIdx + 4] = paletteSpan[b2 >> 4];
                    bufferSpan[dstIdx + 5] = paletteSpan[b2 & 0x0F];
                    bufferSpan[dstIdx + 6] = paletteSpan[b3 >> 4];
                    bufferSpan[dstIdx + 7] = paletteSpan[b3 & 0x0F];
                }
            }
        }

        sink.SubmitFrame(_frameBuffer, 160, 102);
    }
}
