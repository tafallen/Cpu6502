using Cpu6502.Core;

namespace Machines.BbcMicro;

/// <summary>
/// SHEILA hardware MMIO address decoder for the BBC Micro Model B ($FC00–$FEFF).
/// Routes reads and writes to hardware sub-components:
/// - $FE00–$FE07: Motorola 6845 CRTC
/// - $FE08–$FE0F: 8271 Floppy Disc Controller
/// - $FE10–$FE1F: 6850 ACIA / Serial ULA
/// - $FE20–$FE2F: Video ULA palette/mode register
/// - $FE30–$FE3F: ROM Select Latch (IC32)
/// - $FE40–$FE5F: System VIA 6522
/// - $FE60–$FE7F: User VIA 6522
/// </summary>
public sealed class BbcSheilaBus : IBus
{
    private readonly BbcSidewaysRomBank _romBank;
    private readonly byte[] _registers = new byte[0x300];

    public IBus? SystemVia { get; set; }
    public IBus? UserVia { get; set; }
    public IBus? Crtc { get; set; }
    public IBus? Fdc { get; set; }

    public BbcSheilaBus(BbcSidewaysRomBank romBank)
    {
        _romBank = romBank ?? throw new ArgumentNullException(nameof(romBank));
        Crtc = new Mc6845();
    }

    public byte Read(ushort address)
    {
        ushort offset = (ushort)(address & 0xFF);

        // $FE40–$FE5F: System VIA
        if (offset >= 0x40 && offset <= 0x5F && SystemVia is not null)
            return SystemVia.Read((ushort)(offset & 0x0F));

        // $FE60–$FE7F: User VIA
        if (offset >= 0x60 && offset <= 0x7F && UserVia is not null)
            return UserVia.Read((ushort)(offset & 0x0F));

        // $FE00–$FE07: CRTC 6845
        if (offset <= 0x07 && Crtc is not null)
            return Crtc.Read((ushort)(offset & 0x01));

        // $FE08–$FE0F: 8271 FDC
        if (offset >= 0x08 && offset <= 0x0F && Fdc is not null)
            return Fdc.Read((ushort)(offset & 0x07));

        return _registers[offset];
    }

    public void Write(ushort address, byte value)
    {
        ushort offset = (ushort)(address & 0xFF);
        _registers[offset] = value;

        // $FE30–$FE3F: ROM Select Latch (IC32)
        if (offset >= 0x30 && offset <= 0x3F)
        {
            _romBank.SelectBank(value);
            return;
        }

        // $FE40–$FE5F: System VIA
        if (offset >= 0x40 && offset <= 0x5F && SystemVia is not null)
        {
            SystemVia.Write((ushort)(offset & 0x0F), value);
            return;
        }

        // $FE60–$FE7F: User VIA
        if (offset >= 0x60 && offset <= 0x7F && UserVia is not null)
        {
            UserVia.Write((ushort)(offset & 0x0F), value);
            return;
        }

        // $FE00–$FE07: CRTC 6845
        if (offset <= 0x07 && Crtc is not null)
        {
            Crtc.Write((ushort)(offset & 0x01), value);
            return;
        }

        // $FE08–$FE0F: 8271 FDC
        if (offset >= 0x08 && offset <= 0x0F && Fdc is not null)
        {
            Fdc.Write((ushort)(offset & 0x07), value);
            return;
        }
    }
}
