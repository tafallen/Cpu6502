namespace Machines.Pet;

/// <summary>
/// Commodore PET 2001 / 4032 / 8032 keyboard adapter.
/// 10 columns × 8 rows matrix. PIA 6520 Port A outputs column selection (0–9),
/// and PIA 6520 Port B reads back the row key states.
/// </summary>
public sealed class PetKeyboardAdapter
{
    private readonly bool[,] _keyState = new bool[10, 8];

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

    public byte ScanRow(byte selectedCol)
    {
        int col = selectedCol & 0x0F;
        if (col >= 10) return 0xFF;

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
