namespace Machines.Common;

/// <summary>
/// General Instrument AY-3-8910 / AY-3-8912 Programmable Sound Generator (PSG).
/// Reused across Oric-1, Oric Atmos, MSX, ZX Spectrum 128, and Amstrad CPC.
/// </summary>
public class Ay38912
{
    private readonly byte[] _registers = new byte[16];
    private int _selectedRegister;

    public byte ReadRegister()
    {
        if (_selectedRegister < 16)
            return _registers[_selectedRegister];
        return 0xFF;
    }

    public byte ReadData() => ReadRegister();

    public void SelectRegister(byte reg)
    {
        _selectedRegister = reg & 0x0F;
    }

    public void WriteData(byte data)
    {
        if (_selectedRegister < 16)
        {
            _registers[_selectedRegister] = data;
        }
    }

    public ushort GetChannelFrequency(int channel)
    {
        int reg = (channel & 3) * 2;
        return (ushort)(_registers[reg] | ((_registers[reg + 1] & 0x0F) << 8));
    }

    public byte GetChannelVolume(int channel)
    {
        int reg = 8 + (channel & 3);
        return (byte)(_registers[reg] & 0x0F);
    }
}
