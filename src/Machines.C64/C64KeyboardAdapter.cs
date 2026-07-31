namespace Machines.C64;

/// <summary>
/// Commodore 64 8×8 keyboard matrix scanner adapter.
/// High-performance $O(1)$ table-driven row scanner.
/// </summary>
public sealed class C64KeyboardAdapter
{
    private readonly byte[] _matrix = new byte[8];
    private readonly byte[] _rowCache = new byte[256];

    public C64KeyboardAdapter()
    {
        for (int i = 0; i < 8; i++)
            _matrix[i] = 0xFF;

        RebuildCache();
    }

    public void KeyDown(int row, int col)
    {
        if (row >= 0 && row < 8 && col >= 0 && col < 8)
        {
            _matrix[row] &= (byte)~(1 << col);
            RebuildCache();
        }
    }

    public void KeyUp(int row, int col)
    {
        if (row >= 0 && row < 8 && col >= 0 && col < 8)
        {
            _matrix[row] |= (byte)(1 << col);
            RebuildCache();
        }
    }

    public byte ReadRowState(byte praColumnDrive)
    {
        return _rowCache[praColumnDrive];
    }

    private void RebuildCache()
    {
        for (int drive = 0; drive < 256; drive++)
        {
            byte result = 0xFF;
            for (int col = 0; col < 8; col++)
            {
                if ((drive & (1 << col)) == 0)
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
            _rowCache[drive] = result;
        }
    }
}
