namespace Machines.Common;

/// <summary>
/// Texas Instruments SN76489 Programmable Sound Generator (PSG).
/// Reused across BBC Micro, Sega Master System, Game Gear, and ColecoVision.
/// </summary>
public class Sn76489
{
    private readonly ushort[] _registers = new ushort[8];
    private readonly int[] _counters = new int[4];
    private readonly bool[] _outputs = new bool[4];
    private int _latchedRegister;
    private ushort _noiseShiftRegister = 0x4000;

    private static readonly float[] VolumeTable =
    [
        1.0f, 0.794f, 0.631f, 0.501f,
        0.398f, 0.316f, 0.251f, 0.199f,
        0.158f, 0.126f, 0.100f, 0.079f,
        0.063f, 0.050f, 0.040f, 0.0f
    ];

    public ushort GetToneFrequency(int channel) => _registers[(channel & 3) * 2];
    public byte GetVolume(int channel) => (byte)(_registers[(channel & 3) * 2 + 1] & 0x0F);

    public void WriteByte(byte data) => Write(data);

    public void Write(byte data)
    {
        if ((data & 0x80) != 0)
        {
            _latchedRegister = (data >> 4) & 0x07;
            if (_latchedRegister is 0 or 2 or 4)
            {
                _registers[_latchedRegister] = (ushort)((_registers[_latchedRegister] & 0x03F0) | (data & 0x0F));
            }
            else
            {
                _registers[_latchedRegister] = (ushort)(data & 0x0F);
            }
        }
        else
        {
            if (_latchedRegister is 0 or 2 or 4)
            {
                _registers[_latchedRegister] = (ushort)((_registers[_latchedRegister] & 0x000F) | ((data & 0x3F) << 4));
            }
            else
            {
                _registers[_latchedRegister] = (ushort)(data & 0x0F);
            }
        }
    }

    public float GenerateSample()
    {
        float mixed = 0f;

        for (int ch = 0; ch < 3; ch++)
        {
            ushort period = _registers[ch * 2];
            byte vol = (byte)(_registers[ch * 2 + 1] & 0x0F);

            if (period > 5)
            {
                _counters[ch]--;
                if (_counters[ch] <= 0)
                {
                    _counters[ch] = period;
                    _outputs[ch] = !_outputs[ch];
                }
            }

            if (_outputs[ch] && vol < 15)
            {
                mixed += VolumeTable[vol] * 0.25f;
            }
        }

        byte noiseVol = (byte)(_registers[7] & 0x0F);
        if (noiseVol < 15)
        {
            mixed += ((_noiseShiftRegister & 1) != 0 ? VolumeTable[noiseVol] * 0.25f : 0f);
        }

        return mixed;
    }
}
