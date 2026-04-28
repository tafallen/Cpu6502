using Machines.Common;

namespace Machines.Atom;

/// <summary>
/// Translates physical key state into the Acorn Atom's 10×6 keyboard matrix.
///
/// The Atom OS scans the keyboard by writing a value to PPI Port A ($B000):
///   bits 3-0 = row select (0–9)
///   bits 7-4 = VDG gfxmode (ignored here)
/// Port B ($B001) returns active columns, active-low:
///   bits 5-0 = matrix columns for the selected row
///   bit 6    = CTRL key (any row, active-low)
///   bit 7    = SHIFT key (any row, active-low)
///
/// Matrix layout matches the Atomulator reference implementation.
/// </summary>
public sealed class AtomKeyboardAdapter
{
    private readonly IPhysicalKeyboard _keyboard;

    // _matrix[row, col]: row = Port A bits 3-0 (0–9), col = Port B bit (0–5).
    // PhysicalKey.None = unpopulated position (column stays high).
    // Note: SHIFT and CTRL are NOT in this table — they appear in bits 7 and 6.
    private static readonly PhysicalKey[,] _matrix =
    {
        //          col 0                  col 1                col 2                  col 3              col 4              col 5
        /* row 0 */ { PhysicalKey.None,    PhysicalKey.D3,      PhysicalKey.Minus,     PhysicalKey.G,     PhysicalKey.Q,     PhysicalKey.Escape      },
        /* row 1 */ { PhysicalKey.None,    PhysicalKey.D2,      PhysicalKey.Comma,     PhysicalKey.F,     PhysicalKey.P,     PhysicalKey.Z           },
        /* row 2 */ { PhysicalKey.Up,      PhysicalKey.D1,      PhysicalKey.Semicolon, PhysicalKey.E,     PhysicalKey.O,     PhysicalKey.Y           },
        /* row 3 */ { PhysicalKey.Right,   PhysicalKey.D0,      PhysicalKey.Apostrophe,PhysicalKey.D,     PhysicalKey.N,     PhysicalKey.X           },
        /* row 4 */ { PhysicalKey.CapsLock,PhysicalKey.Backspace,PhysicalKey.D9,       PhysicalKey.C,     PhysicalKey.M,     PhysicalKey.W           },
        /* row 5 */ { PhysicalKey.Tab,     PhysicalKey.End,     PhysicalKey.D8,        PhysicalKey.B,     PhysicalKey.L,     PhysicalKey.V           },
        /* row 6 */ { PhysicalKey.RightBracket, PhysicalKey.Return, PhysicalKey.D7,   PhysicalKey.A,     PhysicalKey.K,     PhysicalKey.U           },
        /* row 7 */ { PhysicalKey.Backslash, PhysicalKey.None,  PhysicalKey.D6,        PhysicalKey.Equals,PhysicalKey.J,     PhysicalKey.T           },
        /* row 8 */ { PhysicalKey.LeftBracket, PhysicalKey.None, PhysicalKey.D5,      PhysicalKey.Slash, PhysicalKey.I,     PhysicalKey.S           },
        /* row 9 */ { PhysicalKey.Space,   PhysicalKey.None,    PhysicalKey.D4,        PhysicalKey.Period,PhysicalKey.H,     PhysicalKey.R           },
    };

    private const int Rows = 10;
    private const int Cols = 6;

    public AtomKeyboardAdapter(IPhysicalKeyboard keyboard) => _keyboard = keyboard;

    /// <summary>
    /// Returns the Port B column byte for the row driven on Port A.
    /// Bits 5-0: matrix columns (active-low when key pressed).
    /// Bit 6: CTRL key (active-low).
    /// Bit 7: SHIFT key (active-low, either shift key).
    /// Only bits 3-0 of <paramref name="portA"/> select the row; bits 7-4 are VDG gfxmode.
    /// </summary>
    public byte ScanColumns(byte portA)
    {
        int row = portA & 0x0F;

        byte result = 0xFF;

        if (row < Rows)
        {
            for (int col = 0; col < Cols; col++)
            {
                var key = _matrix[row, col];
                if (key != PhysicalKey.None && _keyboard.IsKeyDown(key))
                    result &= (byte)~(1 << col);
            }
        }

        // SHIFT (bit 7) and CTRL (bit 6) are reported on every row
        if (_keyboard.IsKeyDown(PhysicalKey.LeftShift) || _keyboard.IsKeyDown(PhysicalKey.RightShift))
            result &= 0x7F;
        if (_keyboard.IsKeyDown(PhysicalKey.LeftControl) || _keyboard.IsKeyDown(PhysicalKey.RightControl))
            result &= 0xBF;

        return result;
    }

    /// <summary>
    /// Returns the (row, col) matrix position for a physical key, or (-1, -1) if unmapped.
    /// SHIFT and CTRL return (-1, -1) since they are not in the matrix.
    /// </summary>
    public static (int row, int col) MatrixPosition(PhysicalKey key)
    {
        for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Cols; col++)
                if (_matrix[row, col] == key) return (row, col);
        return (-1, -1);
    }
}
