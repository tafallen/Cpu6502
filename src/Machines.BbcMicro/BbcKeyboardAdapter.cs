using Machines.Common;

namespace Machines.BbcMicro;

/// <summary>
/// BBC Micro Model B keyboard adapter.
/// The BBC keyboard is an 8-row × 10-column matrix. Column selection is driven by
/// System VIA Port A (IC32 latch bits [3:0]), and row states are read back on System VIA Port A.
/// </summary>
public sealed class BbcKeyboardAdapter
{
    private readonly bool[,] _keyState = new bool[10, 8]; // 10 columns, 8 rows

    public void KeyDown(int col, int row)
    {
        if (col >= 0 && col < 10 && row >= 0 && row < 8)
            _keyState[col, row] = true;
    }

    public void KeyUp(int col, int row)
    {
        if (col >= 0 && col < 10 && row >= 0 && row < 8)
            _keyState[col, row] = false;
    }

    public byte ScanColumn(byte selectedCol)
    {
        int col = selectedCol & 0x0F;
        if (col >= 10) return 0xFF;

        byte result = 0xFF;
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
