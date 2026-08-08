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
    private readonly Cartridge? _cartridge;

    public Suzy(Cartridge? cartridge = null)
    {
        _cartridge = cartridge;
    }

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
            0x88 => 0x01, // SUZYHREV
            0x92 => 0x00, // SPRSYS (Bit 0 = SPRBUSY: 0 = idle)
            0xB2 => _cartridge?.ReadBank0() ?? 0xFF,
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
            case 0x91: // SPRGO - Start Sprite Blitter DMA
                if ((value & 0x01) != 0)
                {
                    ProcessBlit();
                }
                break;
        }
    }

    public Ram? Ram { get; set; }
    public Mikey? Mikey { get; set; }

    private int _lastX;
    private int _lastY;

    private void ProcessBlit()
    {
        if (Ram?.DirectWriteBuffer is not byte[] ramBuf) return;

        ushort scbPtr = (ushort)(_registers[0x10] | (_registers[0x11] << 8)); // SPRINIT / SCB Ptr
        int limit = 512; // Safeguard against circular SCB linked lists

        ushort dispAddr = Mikey is not null
            ? (ushort)(Mikey.Read(0x94) | (Mikey.Read(0x95) << 8))
            : (ushort)0x0400;

        if (dispAddr == 0) dispAddr = 0x0400;

        byte[] paletteMap = new byte[16];
        for (int i = 0; i < 16; i++) paletteMap[i] = (byte)i;

        while (scbPtr != 0 && scbPtr < ramBuf.Length - 10 && --limit > 0)
        {
            byte sprctl0 = ramBuf[scbPtr];
            byte sprctl1 = ramBuf[scbPtr + 1];
            byte sprcoll = ramBuf[scbPtr + 2];
            ushort nextScb = (ushort)(ramBuf[scbPtr + 3] | (ramBuf[scbPtr + 4] << 8));

            int ptr = scbPtr + 5;

            // Bit 2 of SPRCTL0 determines if DataPtr is present
            ushort dataPtr = (ushort)(ramBuf[ptr] | (ramBuf[ptr + 1] << 8));
            ptr += 2;

            // Check reload mode: SPRCTL1 bits 4..5 control PosX/PosY reload, SPRCTL0 bits 4..5 control Scale/Color
            int posReload = (sprctl1 >> 4) & 0x03;
            int scaleReload = (sprctl0 >> 4) & 0x03;

            int startX = _lastX;
            int startY = _lastY;

            if (posReload != 0 && ptr + 4 <= ramBuf.Length) // Reload PosX & PosY
            {
                startX = (short)(ramBuf[ptr] | (ramBuf[ptr + 1] << 8));
                startY = (short)(ramBuf[ptr + 2] | (ramBuf[ptr + 3] << 8));
                ptr += 4;
                _lastX = startX;
                _lastY = startY;
            }

            if (scaleReload != 0) // Reload ScaleX and optional ScaleY
            {
                ptr += 2; // Skip ScaleX
                if ((sprctl0 & 0x01) != 0) ptr += 2; // Skip ScaleY if 2D scaling
            }

            // Reload SCB 16-color palette map if bit 3 of SPRCTL1 is set
            if ((sprctl1 & 0x08) != 0 && ptr + 8 <= ramBuf.Length)
            {
                for (int i = 0; i < 8; i++)
                {
                    byte pair = ramBuf[ptr++];
                    paletteMap[i * 2]     = (byte)(pair >> 4);
                    paletteMap[i * 2 + 1] = (byte)(pair & 0x0F);
                }
            }

            // Decode 4-bit RLE / Literal nibble packet stream into video DRAM
            int currData = dataPtr;
            int y = startY;

            while (currData < ramBuf.Length - 1)
            {
                byte scanlineBytes = ramBuf[currData++];
                if (scanlineBytes == 0x00 || scanlineBytes == 0x01) break; // End of sprite data stream

                int lineEnd = currData + (scanlineBytes & 0x7F) - 1;
                int x = startX;

                while (currData < lineEnd && currData < ramBuf.Length)
                {
                    byte packetHeader = ramBuf[currData++];
                    int count = (packetHeader & 0x0F) + 1;
                    int packetType = (packetHeader >> 4) & 0x0F;

                    if ((packetType & 0x08) == 0) // Literal packet: copy 'count' nibbles
                    {
                        for (int i = 0; i < count && currData < ramBuf.Length; i++)
                        {
                            byte nibble = (i & 1) == 0 ? (byte)(ramBuf[currData] >> 4) : (byte)(ramBuf[currData++] & 0x0F);
                            PlotPixel(ramBuf, dispAddr, x++, y, paletteMap[nibble]);
                        }
                        if ((count & 1) != 0 && currData < ramBuf.Length) currData++; // Align byte boundary
                    }
                    else if ((packetType & 0x04) != 0) // Repeat packet: repeat single nibble 'count' times
                    {
                        if (currData < ramBuf.Length)
                        {
                            byte repeatNibble = (byte)(ramBuf[currData++] >> 4);
                            byte mappedColor = paletteMap[repeatNibble];
                            for (int i = 0; i < count; i++)
                            {
                                PlotPixel(ramBuf, dispAddr, x++, y, mappedColor);
                            }
                        }
                    }
                    else // Zero-run packet: skip 'count' transparent pixels
                    {
                        x += count;
                    }
                }
                currData = lineEnd;
                y++;
            }

            scbPtr = nextScb;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PlotPixel(byte[] ramBuf, ushort dispAddr, int x, int y, byte colorIndex)
    {
        if (colorIndex == 0) return; // 0 = Transparent
        if (x < 0 || x >= 160 || y < 0 || y >= 102) return;

        int fbIndex = dispAddr + (y * 80) + (x >> 1);
        if (fbIndex < ramBuf.Length)
        {
            if ((x & 1) == 0)
                ramBuf[fbIndex] = (byte)((ramBuf[fbIndex] & 0x0F) | (colorIndex << 4));
            else
                ramBuf[fbIndex] = (byte)((ramBuf[fbIndex] & 0xF0) | colorIndex);
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
