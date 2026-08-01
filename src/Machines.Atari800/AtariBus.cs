using Cpu6502.Core;
using Machines.Common;

namespace Machines.Atari800;

/// <summary>
/// Atari 800XL Memory Bus router handling 64 KB RAM, PIA 6520 PORTB banking, GTIA, ANTIC, POKEY, and OS ROM.
/// High-performance $O(1)$ page router.
/// </summary>
public sealed class AtariBus : IBus
{
    public Ram Ram { get; } = new(0x10000); // 64 KB RAM
    public Gtia Gtia { get; } = new(); // GTIA at $D000–$D0FF
    public Pokey Pokey { get; } = new(); // POKEY at $D200–$D2FF
    public Pia6520 Pia { get; } = new(); // PIA at $D300–$D3FF
    public Antic Antic { get; } = new(); // ANTIC at $D400–$D4FF
    public byte[] OsRom { get; } = new byte[0x4000]; // 16 KB OS ROM ($C000–$FFFF)
    public byte[] BasicRom { get; } = new byte[0x2000]; // 8 KB BASIC ROM ($A000–$BFFF)

    public bool OsRomEnabled => (Pia.PortB & 0x01) == 0; // Bit 0 = 0: OS ROM enabled
    public bool BasicRomEnabled => (Pia.PortB & 0x02) == 0; // Bit 1 = 0: BASIC ROM enabled

    public byte Read(ushort address)
    {
        byte page = (byte)(address >> 8);

        // Hardware I/O ($D000–$D7FF)
        if (page >= 0xD0 && page <= 0xD7)
        {
            if (page == 0xD0) return Gtia.Read(address);
            if (page == 0xD2) return Pokey.Read(address);
            if (page == 0xD3) return Pia.Read(address);
            if (page == 0xD4) return Antic.Read(address);

            return Ram.Read(address);
        }

        // BASIC ROM ($A000–$BFFF)
        if (page >= 0xA0 && page <= 0xBF && BasicRomEnabled)
        {
            return BasicRom[address - 0xA000];
        }

        // OS ROM ($C000–$FFFF excluding $D000–$D7FF)
        if (page >= 0xC0 && OsRomEnabled)
        {
            return OsRom[address - 0xC000];
        }

        return Ram.Read(address);
    }

    public void Write(ushort address, byte value)
    {
        byte page = (byte)(address >> 8);

        if (page >= 0xD0 && page <= 0xD7)
        {
            if (page == 0xD0) Gtia.Write(address, value);
            else if (page == 0xD2) Pokey.Write(address, value);
            else if (page == 0xD3) Pia.Write(address, value);
            else if (page == 0xD4) Antic.Write(address, value);
        }

        Ram.Write(address, value);
    }
}
