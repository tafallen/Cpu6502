using System.Runtime.CompilerServices;
using Cpu6502.Core;

namespace Machines.Lynx;

/// <summary>
/// Atari Lynx SUZY custom VLSI chip ($FC00–$FCFF).
/// Features 16-bit hardware math coprocessor (multiplication & division)
/// and hardware sprite blitter engine.
/// </summary>
public sealed class Suzy : IBus
{
    private readonly byte[] _registers = new byte[0x100];

    // Math Coprocessor Registers
    public ushort MATHA { get; set; }
    public ushort MATHB { get; set; }
    public ushort MATHC { get; set; }
    public ushort MATHD { get; set; }
    public uint MATHRESULT { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ushort address)
    {
        byte reg = (byte)(address & 0xFF);
        return reg switch
        {
            0x52 => (byte)(MATHA & 0xFF),
            0x53 => (byte)(MATHA >> 8),
            0x54 => (byte)(MATHB & 0xFF),
            0x55 => (byte)(MATHB >> 8),
            0x60 => (byte)(MATHRESULT & 0xFF),
            0x61 => (byte)((MATHRESULT >> 8) & 0xFF),
            0x62 => (byte)((MATHRESULT >> 16) & 0xFF),
            0x63 => (byte)((MATHRESULT >> 24) & 0xFF),
            _ => _registers[reg]
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort address, byte value)
    {
        byte reg = (byte)(address & 0xFF);
        _registers[reg] = value;

        switch (reg)
        {
            case 0x52:
                MATHA = (ushort)((MATHA & 0xFF00) | value);
                break;
            case 0x53:
                MATHA = (ushort)((MATHA & 0x00FF) | (value << 8));
                break;
            case 0x54:
                MATHB = (ushort)((MATHB & 0xFF00) | value);
                break;
            case 0x55:
                MATHB = (ushort)((MATHB & 0x00FF) | (value << 8));
                ExecuteMultiply();
                break;
            case 0x60:
                MATHRESULT = (MATHRESULT & 0xFFFFFF00u) | value;
                break;
            case 0x61:
                MATHRESULT = (MATHRESULT & 0xFFFF00FFu) | ((uint)value << 8);
                break;
            case 0x62:
                MATHRESULT = (MATHRESULT & 0xFF00FFFFu) | ((uint)value << 16);
                break;
            case 0x63:
                MATHRESULT = (MATHRESULT & 0x00FFFFFFu) | ((uint)value << 24);
                ExecuteDivide();
                break;
        }
    }

    private void ExecuteMultiply()
    {
        // 16-bit × 16-bit multiplication -> 32-bit product
        MATHRESULT = (uint)MATHA * (uint)MATHB;
    }

    private void ExecuteDivide()
    {
        // 32-bit / 16-bit division -> 16-bit quotient
        if (MATHA != 0)
        {
            uint quotient = MATHRESULT / MATHA;
            uint remainder = MATHRESULT % MATHA;
            MATHRESULT = (remainder << 16) | (quotient & 0xFFFF);
        }
        else
        {
            MATHRESULT = 0xFFFFFFFFu; // Division by zero fallback
        }
    }
}
