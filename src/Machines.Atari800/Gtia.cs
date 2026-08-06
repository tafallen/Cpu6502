using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cpu6502.Core;

namespace Machines.Atari800;

/// <summary>
/// Atari GTIA graphics engine ($D000–$D01F).
/// Optimized with pre-computed 256-color RGBA palette cache.
/// </summary>
public sealed class Gtia : IBus
{
    private readonly byte[] _registers = new byte[0x20];
    private readonly uint[] _palette = new uint[256];

    public Gtia()
    {
        InitializePalette();
    }

    private void InitializePalette()
    {
        for (int i = 0; i < 256; i++)
        {
            int hue = (i >> 4) & 0x0F;
            int lum = (i & 0x0F) * 17;

            byte r = (byte)Math.Clamp(lum + (hue == 3 || hue == 4 ? 40 : 0), 0, 255);
            byte g = (byte)Math.Clamp(lum + (hue == 7 || hue == 8 ? 40 : 0), 0, 255);
            byte b = (byte)Math.Clamp(lum + (hue == 11 || hue == 12 ? 40 : 0), 0, 255);

            _palette[i] = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetColor(byte index)
    {
        ref byte regRef = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(_registers);
        ref uint palRef = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(_palette);
        byte colorIdx = Unsafe.Add(ref regRef, 0x16 + (index & 0x08));
        return Unsafe.Add(ref palRef, colorIdx);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ushort address)
    {
        byte reg = (byte)(address & 0x1F);
        switch (reg)
        {
            case 0x00: return 0x00;
            case 0x04: return 0x00;
            case 0x08: return 0x00;
            case 0x0C: return 0x00;
            case 0x10: return 0x0F;
            case 0x1F: return 0x07;
            default: return _registers[reg];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort address, byte value)
    {
        byte reg = (byte)(address & 0x1F);
        _registers[reg] = value;

        if (reg == 0x1E)
        {
            _registers.AsSpan(0, 16).Clear();
        }
    }
}
