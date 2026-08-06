using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cpu6502.Core;
using Machines.Common;

namespace Machines.Atari800;

/// <summary>
/// Atari 800XL Memory Bus router handling 64 KB RAM, PIA 6520 PORTB banking, GTIA, ANTIC, POKEY, and OS ROM.
/// Ultra-high performance $O(1)$ page lookup router with direct span/ref access.
/// </summary>
public sealed class AtariBus : IBus
{
    public Ram Ram { get; } = new(0x10000);
    public Gtia Gtia { get; } = new();
    public Pokey Pokey { get; } = new();
    public Pia6520 Pia { get; } = new();
    public Antic Antic { get; } = new();
    public byte[] OsRom { get; } = new byte[0x4000];
    public byte[] BasicRom { get; } = new byte[0x2000];

    private readonly byte[] _ramBuf;
    private readonly byte[] _basicRomBuf;
    private readonly byte[] _osRomBuf;

    public AtariBus()
    {
        _ramBuf = Ram.DirectWriteBuffer!;
        _basicRomBuf = BasicRom;
        _osRomBuf = OsRom;
    }

    public bool OsRomEnabled => (Pia.PortB & 0x01) == 0;
    
    public bool BasicRomEnabled => (Pia.PortB & 0x02) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ushort address)
    {
        byte page = (byte)(address >> 8);

        // Fast path: Hardware I/O ($D000–$D7FF)
        if ((page & 0xF8) == 0xD0)
        {
            if (page == 0xD0) return Gtia.Read(address);
            if (page == 0xD2) return Pokey.Read(address);
            if (page == 0xD3) return Pia.Read(address);
            if (page == 0xD4) return Antic.Read(address);

            return _ramBuf[address];
        }

        // BASIC ROM ($A000–$BFFF)
        if ((page & 0xE0) == 0xA0 && BasicRomEnabled)
        {
            return _basicRomBuf[address - 0xA000];
        }

        // OS ROM ($C000–$FFFF excluding I/O)
        if (page >= 0xC0 && OsRomEnabled)
        {
            return _osRomBuf[address - 0xC000];
        }

        return _ramBuf[address];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort address, byte value)
    {
        byte page = (byte)(address >> 8);

        if ((page & 0xF8) == 0xD0)
        {
            if (page == 0xD0) Gtia.Write(address, value);
            else if (page == 0xD2) Pokey.Write(address, value);
            else if (page == 0xD3) Pia.Write(address, value);
            else if (page == 0xD4) Antic.Write(address, value);
        }

        _ramBuf[address] = value;
    }
}
