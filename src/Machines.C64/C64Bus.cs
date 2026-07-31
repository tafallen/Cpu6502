using Cpu6502.Core;
using Machines.Common;

namespace Machines.C64;

/// <summary>
/// C64 Memory Bus controller handling MOS 6510 $00/$01 banking.
/// High-performance $O(1)$ page-table router.
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
    public byte PortData { get; set; } = 0x37;

    public bool Loram => (PortData & 0x01) != 0;
    public bool Hiram => (PortData & 0x02) != 0;
    public bool Charen => (PortData & 0x04) != 0;

    public byte Read(ushort address)
    {
        byte page = (byte)(address >> 8);

        if (address <= 0x0001)
            return address == 0x0000 ? PortDirection : PortData;

        // BASIC ROM ($A000–$BFFF)
        if (page >= 0xA0 && page <= 0xBF && Loram && Hiram)
            return BasicRom[address - 0xA000];

        // Character ROM vs I/O ($D000–$DFFF)
        if (page >= 0xD0 && page <= 0xDF)
        {
            if (!Charen && (Loram || Hiram))
                return CharRom[address - 0xD000];

            if (page <= 0xD3) return Vic.Read(address);
            if (page >= 0xD4 && page <= 0xD7) return Sid.Read(address);
            if (page == 0xDC) return Cia1.Read(address);
            if (page == 0xDD) return Cia2.Read(address);

            return Ram.Read(address);
        }

        // KERNAL ROM ($E000–$FFFF)
        if (page >= 0xE0 && Hiram)
            return KernalRom[address - 0xE000];

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

        byte page = (byte)(address >> 8);
        if (page >= 0xD0 && page <= 0xDF)
        {
            if (page <= 0xD3) Vic.Write(address, value);
            else if (page >= 0xD4 && page <= 0xD7) Sid.Write(address, value);
            else if (page == 0xDC) Cia1.Write(address, value);
            else if (page == 0xDD) Cia2.Write(address, value);
        }

        Ram.Write(address, value);
    }
}
