using Cpu6502.Core;

namespace Machines.Atari800;

/// <summary>
/// Atari GTIA (Graphics Television Interface Adapter) chip ($D000–$D01F).
/// Features 256-color palette registers, 4 player + 4 missile hardware sprites,
/// and hardware collision detection registers.
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
        // Generate Atari 256-color NTSC/PAL palette
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

    public uint GetColor(byte index) => _palette[_registers[0x16 + (index & 0x08)]];

    public byte Read(ushort address)
    {
        byte reg = (byte)(address & 0x1F);
        switch (reg)
        {
            case 0x00: return 0x00; // M0PF (Missile 0 to Playfield collision)
            case 0x04: return 0x00; // P0PF (Player 0 to Playfield collision)
            case 0x08: return 0x00; // M0PL (Missile 0 to Player collision)
            case 0x0C: return 0x00; // P0PL (Player 0 to Player collision)
            case 0x10: return 0x0F; // TRIG0 (Console trigger button 0)
            case 0x1F: return 0x07; // CONSOL (Console switches: OPTION/SELECT/START)
            default: return _registers[reg];
        }
    }

    public void Write(ushort address, byte value)
    {
        byte reg = (byte)(address & 0x1F);
        _registers[reg] = value;

        if (reg == 0x1E) // HITCLR (Clear collisions)
        {
            for (int i = 0; i < 16; i++) _registers[i] = 0;
        }
    }
}
