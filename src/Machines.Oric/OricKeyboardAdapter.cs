namespace Machines.Oric;

/// <summary>
/// Oric-1 / Oric Atmos keyboard adapter.
/// Uses an 8-row × 8-column key matrix connected to the MOS 6522 VIA and AY-3-8912 PSG.
/// Column index is written to PSG Port A; row state is read from VIA Port B.
/// </summary>
public sealed class OricKeyboardAdapter
{
    private readonly bool[,] _keyState = new bool[8, 8];

    public void KeyDown(int col, int row)
    {
        if (col >= 0 && col < 8 && row >= 0 && row < 8)
            _keyState[col, row] = true;
    }

    public void KeyUp(int col, int row)
    {
        if (col >= 0 && col < 8 && row >= 0 && row < 8)
            _keyState[col, row] = false;
    }

    public byte ScanColumn(byte selectedCol)
    {
        int col = selectedCol & 0x07;
        byte result = 0xFF; // 1 = unpressed
        for (int r = 0; r < 8; r++)
        {
            if (_keyState[col, r])
            {
                result &= (byte)~(1 << r); // 0 = pressed
            }
        }
        return result;
    }
}
