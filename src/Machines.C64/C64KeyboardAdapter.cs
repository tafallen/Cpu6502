using Machines.Common;

namespace Machines.C64;

/// <summary>
/// Commodore 64 8×8 keyboard matrix scanner adapter.
/// Connects CIA1 Port A (Output column drive) to Port B (Input row sense).
/// </summary>
public sealed class C64KeyboardAdapter
{
    private readonly byte[] _matrix = new byte[8];

    public C64KeyboardAdapter()
    {
        for (int i = 0; i < 8; i++)
            _matrix[i] = 0xFF; // 1 = Key released, 0 = Key pressed
    }

    public void KeyDown(int row, int col)
    {
        if (row >= 0 && row < 8 && col >= 0 && col < 8)
            _matrix[row] &= (byte)~(1 << col);
    }

    public void KeyUp(int row, int col)
    {
        if (row >= 0 && row < 8 && col >= 0 && col < 8)
            _matrix[row] |= (byte)(1 << col);
    }

    public byte ReadRowState(byte praColumnDrive)
    {
        byte result = 0xFF;
        for (int col = 0; col < 8; col++)
        {
            if ((praColumnDrive & (1 << col)) == 0) // Column driven LOW
            {
                for (int row = 0; row < 8; row++)
                {
                    if ((_matrix[row] & (1 << col)) == 0)
                    {
                        result &= (byte)~(1 << row);
                    }
                }
            }
        }
        return result;
    }
}
