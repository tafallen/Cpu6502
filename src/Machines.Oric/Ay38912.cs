namespace Machines.Oric;

/// <summary>
/// General Instrument AY-3-8912 Programmable Sound Generator (PSG) used in Oric-1 / Oric Atmos.
/// Features 3 square-wave tone channels (A, B, C), 1 noise generator, and 1 envelope generator.
/// Managed via 16 8-bit registers (R0–R15).
/// </summary>
public sealed class Ay38912
{
    private readonly byte[] _registers = new byte[16];
    private int _selectedRegister;

    private static readonly float[] VolumeTable =
    [
        0.0f, 0.01f, 0.02f, 0.03f,
        0.05f, 0.08f, 0.12f, 0.18f,
        0.26f, 0.35f, 0.48f, 0.62f,
        0.75f, 0.88f, 0.95f, 1.00f
    ];

    public byte ReadRegister()
    {
        if (_selectedRegister < 16)
            return _registers[_selectedRegister];
        return 0xFF;
    }

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
