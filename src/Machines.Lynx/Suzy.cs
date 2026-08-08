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

    private void ProcessBlit()
    {
        if (Ram?.DirectWriteBuffer is not byte[] ramBuf) return;

        ushort scbPtr = (ushort)(_registers[0x10] | (_registers[0x11] << 8)); // SPRINIT / SCB Ptr
        int limit = 512; // Protection against infinite linked list loops

        ushort dispAddr = Mikey is not null
            ? (ushort)(Mikey.Read(0x94) | (Mikey.Read(0x95) << 8))
            : (ushort)0x2000;

        if (dispAddr == 0) dispAddr = 0x2000;

        while (scbPtr != 0 && scbPtr < ramBuf.Length - 10 && --limit > 0)
        {
            byte sprctl0 = ramBuf[scbPtr];
            byte sprctl1 = ramBuf[scbPtr + 1];
            byte sprcoll = ramBuf[scbPtr + 2];
            ushort nextScb = (ushort)(ramBuf[scbPtr + 3] | (ramBuf[scbPtr + 4] << 8));
            ushort dataPtr = (ushort)(ramBuf[scbPtr + 5] | (ramBuf[scbPtr + 6] << 8));

            int startX = (short)(ramBuf[scbPtr + 7] | (ramBuf[scbPtr + 8] << 8));
            int startY = (short)(ramBuf[scbPtr + 9] | (ramBuf[scbPtr + 10] << 8));

            // Decode RLE / literal 4-bit nibble sprite data into framebuffer
            int currData = dataPtr;
            int y = startY;

            while (currData < ramBuf.Length - 2)
            {
                byte lineHeader = ramBuf[currData++];
                if (lineHeader == 0x00 || lineHeader == 0x01) break; // End of sprite data

                int bytesInLine = lineHeader & 0x7F;
                int x = startX;

                for (int b = 0; b < bytesInLine && currData < ramBuf.Length; b++)
                {
                    byte pixelByte = ramBuf[currData++];
                    byte pixel1 = (byte)(pixelByte >> 4);
                    byte pixel2 = (byte)(pixelByte & 0x0F);

                    if (y >= 0 && y < 102)
                    {
                        if (x >= 0 && x < 160)
                        {
                            int fbIndex = dispAddr + (y * 80) + (x >> 1);
                            if (fbIndex < ramBuf.Length)
                            {
                                if ((x & 1) == 0)
                                    ramBuf[fbIndex] = (byte)((ramBuf[fbIndex] & 0x0F) | (pixel1 << 4));
                                else
                                    ramBuf[fbIndex] = (byte)((ramBuf[fbIndex] & 0xF0) | pixel1);
                            }
                        }
                        if (x + 1 >= 0 && x + 1 < 160)
                        {
                            int fbIndex = dispAddr + (y * 80) + ((x + 1) >> 1);
                            if (fbIndex < ramBuf.Length)
                            {
                                if (((x + 1) & 1) == 0)
                                    ramBuf[fbIndex] = (byte)((ramBuf[fbIndex] & 0x0F) | (pixel2 << 4));
                                else
                                    ramBuf[fbIndex] = (byte)((ramBuf[fbIndex] & 0xF0) | pixel2);
                            }
                        }
                    }
                    x += 2;
                }
                y++;
            }

            scbPtr = nextScb;
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
