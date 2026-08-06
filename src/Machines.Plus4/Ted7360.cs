using System.Runtime.CompilerServices;
using Cpu6502.Core;
using Machines.Common;

namespace Machines.Plus4;

/// <summary>
/// MOS 7360 / 8360 TED (Text Editing Device) custom integrated circuit ($FF00–$FF3F).
/// Combines Video generator (320×200 text/bitmap, 121 colors), 2-channel Audio synthesizer,
/// 2 16-bit Hardware Timers, and Interrupt controller.
/// </summary>
public sealed class Ted7360 : IBus
{
    private readonly byte[] _registers = new byte[0x40];
    private readonly uint[] _palette = new uint[128]; // 16 hues × 8 luminances
    private readonly uint[] _frameBuffer = new uint[320 * 200];

    // Timers
    public ushort Timer1 { get; private set; }
    public ushort Timer2 { get; private set; }
    public ushort Timer1Latch { get; set; } = 0xFFFF;
    public ushort Timer2Latch { get; set; } = 0xFFFF;

    // Interrupts
    public byte IrqStatus { get; private set; }
    public byte IrqEnable { get; private set; }
    public bool Irq => (IrqStatus & IrqEnable & 0x7E) != 0;

    // Keyboard & I/O
    public byte KeyboardLatch { get; set; } = 0xFF;

    private readonly IAudioSink? _audio;

    public Ted7360(IAudioSink? audio = null)
    {
        _audio = audio;
        InitializePalette();
    }

    private void InitializePalette()
    {
        // 121 colors: 16 hues × 8 luminance levels
        for (int hue = 0; hue < 16; hue++)
        {
            for (int lum = 0; lum < 8; lum++)
            {
                int idx = (lum << 4) | hue;
                float brightness = lum / 7.0f;

                byte r, g, b;
                if (hue == 0) // Black/White monochrome hue
                {
                    r = g = b = (byte)(brightness * 255);
                }
                else
                {
                    float angle = (hue - 1) * (360.0f / 15.0f) * (MathF.PI / 180.0f);
                    r = (byte)Math.Clamp((brightness * 255) + (MathF.Sin(angle) * 50), 0, 255);
                    g = (byte)Math.Clamp((brightness * 255) + (MathF.Cos(angle) * 50), 0, 255);
                    b = (byte)Math.Clamp((brightness * 255) - (MathF.Sin(angle) * 50), 0, 255);
                }

                _palette[idx & 0x7F] = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ushort address)
    {
        byte reg = (byte)(address & 0x3F);
        return reg switch
        {
            0x00 => (byte)(Timer1 & 0xFF),
            0x01 => (byte)(Timer1 >> 8),
            0x02 => (byte)(Timer2 & 0xFF),
            0x03 => (byte)(Timer2 >> 8),
            0x09 => (byte)(IrqStatus | (Irq ? 0x80 : 0x00)),
            0x0A => IrqEnable,
            0x08 => KeyboardLatch,
            _ => _registers[reg]
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort address, byte value)
    {
        byte reg = (byte)(address & 0x3F);
        _registers[reg] = value;

        switch (reg)
        {
            case 0x00:
                Timer1Latch = (ushort)((Timer1Latch & 0xFF00) | value);
                break;
            case 0x01:
                Timer1Latch = (ushort)((Timer1Latch & 0x00FF) | (value << 8));
                Timer1 = Timer1Latch; // Latch reload
                break;
            case 0x02:
                Timer2Latch = (ushort)((Timer2Latch & 0xFF00) | value);
                break;
            case 0x03:
                Timer2Latch = (ushort)((Timer2Latch & 0x00FF) | (value << 8));
                Timer2 = Timer2Latch; // Latch reload
                break;
            case 0x09: // Write to clear IRQ flags
                IrqStatus &= (byte)~value;
                break;
            case 0x0A: // IRQ Enable mask
                IrqEnable = value;
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Tick(int cycles = 1)
    {
        // Decrement Timer 1
        if (Timer1 <= cycles)
        {
            Timer1 = (ushort)(Timer1Latch - (cycles - Timer1));
            IrqStatus |= 0x02; // Timer 1 IRQ bit 1
        }
        else
        {
            Timer1 -= (ushort)cycles;
        }

        // Decrement Timer 2
        if (Timer2 <= cycles)
        {
            Timer2 = (ushort)(Timer2Latch - (cycles - Timer2));
            IrqStatus |= 0x04; // Timer 2 IRQ bit 2
        }
        else
        {
            Timer2 -= (ushort)cycles;
        }
    }

    public void RenderFrame(Ram ram, IVideoSink sink)
    {
        byte[]? ramBuf = ram.DirectWriteBuffer;
        if (ramBuf is null) return;

        uint bgColor = _palette[_registers[0x15] & 0x7F];
        uint fgColor = _palette[_registers[0x14] & 0x7F];

        Span<uint> bufferSpan = _frameBuffer;
        bufferSpan.Fill(bgColor);

        // Simple 40×25 text mode renderer from $0C00 (Video RAM)
        ushort videoRam = 0x0C00;
        ushort charGenRam = 0x3000;

        for (int row = 0; row < 25; row++)
        {
            for (int col = 0; col < 40; col++)
            {
                byte charCode = ramBuf[videoRam + (row * 40 + col)];
                int pixelBase = (row * 8) * 320 + (col * 8);

                for (int py = 0; py < 8; py++)
                {
                    byte glyphLine = ramBuf[charGenRam + (charCode * 8 + py)];
                    int pixelOffset = pixelBase + py * 320;

                    for (int px = 0; px < 8; px++)
                    {
                        bool pixelSet = (glyphLine & (0x80 >> px)) != 0;
                        bufferSpan[pixelOffset + px] = pixelSet ? fgColor : bgColor;
                    }
                }
            }
        }

        sink.SubmitFrame(_frameBuffer, 320, 200);
    }
}
