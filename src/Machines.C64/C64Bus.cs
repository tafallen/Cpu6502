using Cpu6502.Core;
using Machines.Common;

namespace Machines.C64;

/// <summary>
/// C64 Memory Bus controller handling MOS 6510 $00/$01 banking.
/// Port Data Direction Register ($00): Default 0x2F (Bits 0-2 output)
/// Port Data Register ($01):
///   Bit 0 (LORAM):  1 = BASIC ROM ($A000–$BFFF), 0 = RAM
///   Bit 1 (HIRAM):  1 = KERNAL ROM ($E000–$FFFF), 0 = RAM
///   Bit 2 (CHAREN): 1 = I/O Regs ($D000–$DFFF), 0 = Character ROM
/// </summary>
public sealed class C64Bus : IBus
{
    public Ram Ram { get; } = new(0x10000); // 64 KB RAM
    public Vic2Video Vic { get; } = new(); // VIC-II at $D000
    public Sid6581 Sid { get; } = new(); // SID at $D400
    public Cia6526 Cia1 { get; } = new(); // CIA1 at $DC00 (Keyboard, Joystick 2, IRQ)
    public Cia6526 Cia2 { get; } = new(); // CIA2 at $DD00 (VIC-II Banking, NMI)
    public byte[] BasicRom { get; } = new byte[0x2000]; // 8 KB BASIC ROM ($A000–$BFFF)
    public byte[] KernalRom { get; } = new byte[0x2000]; // 8 KB KERNAL ROM ($E000–$FFFF)
    public byte[] CharRom { get; } = new byte[0x1000]; // 4 KB Character ROM ($D000–$DFFF)

    public byte PortDirection { get; set; } = 0x2F;
    public byte PortData { get; set; } = 0x37; // Default: LORAM=1, HIRAM=1, CHAREN=1 (BASIC, KERNAL, I/O mapped)

    public bool Loram => (PortData & 0x01) != 0;
    public bool Hiram => (PortData & 0x02) != 0;
    public bool Charen => (PortData & 0x04) != 0;

    public byte Read(ushort address)
    {
        if (address == 0x0000)
            return PortDirection;

        if (address == 0x0001)
            return PortData;

        // BASIC ROM ($A000–$BFFF)
        if (address >= 0xA000 && address <= 0xBFFF && Loram && Hiram)
        {
            return BasicRom[address - 0xA000];
        }

        // Character ROM vs I/O ($D000–$DFFF)
        if (address >= 0xD000 && address <= 0xDFFF)
        {
            if (!Charen && (Loram || Hiram))
            {
                return CharRom[address - 0xD000];
            }

            if (address >= 0xD000 && address <= 0xD03F)
                return Vic.Read(address);

            if (address >= 0xD400 && address <= 0xD7FF)
                return Sid.Read(address);

            if (address >= 0xDC00 && address <= 0xDCFF)
                return Cia1.Read(address);

            if (address >= 0xDD00 && address <= 0xDDFF)
                return Cia2.Read(address);

            return Ram.Read(address);
        }

        // KERNAL ROM ($E000–$FFFF)
        if (address >= 0xE000 && address <= 0xFFFF && Hiram)
        {
            return KernalRom[address - 0xE000];
        }

        return Ram.Read(address);
    }

    public void Write(ushort address, byte value)
    {
        if (address == 0x0000)
        {
            PortDirection = value;
            return;
        }

        if (address == 0x0001)
        {
            PortData = value;
            return;
        }

        if (address >= 0xD000 && address <= 0xD03F)
        {
            Vic.Write(address, value);
        }
        else if (address >= 0xD400 && address <= 0xD7FF)
        {
            Sid.Write(address, value);
        }
        else if (address >= 0xDC00 && address <= 0xDCFF)
        {
            Cia1.Write(address, value);
        }
        else if (address >= 0xDD00 && address <= 0xDDFF)
        {
            Cia2.Write(address, value);
        }

        // CPU writes always go to underlying RAM regardless of ROM mapping
        Ram.Write(address, value);
    }
}
