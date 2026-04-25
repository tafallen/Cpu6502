using Machines.Common;

namespace Machines.Atom;

/// <summary>
/// Translates physical key state into the Acorn Atom's 10×6 keyboard matrix.
///
/// The Atom OS scans the keyboard by writing a row number (0–9) to PPI Port A
/// at $B000, then reading the active columns from Port B bits 0–5 at $B001.
/// A column bit is LOW when the key at that (row, col) position is pressed.
///
/// <see cref="ScanColumns"/> is wired directly to the PPI's ReadPortB delegate:
///   ppi.ReadPortB = () => adapter.ScanColumns(ppi.PortALatch);
///
/// Matrix source: Acorn Atom keyboard matrix diagram, acornatom.nl.
/// </summary>
public sealed class AtomKeyboardAdapter
{
    private readonly IPhysicalKeyboard _keyboard;

    // _matrix[row, col]: row = value written to Port A (0–9), col = Port B bit (0–5).
    // PhysicalKey.None = no key at that position (column stays high).
    private static readonly PhysicalKey[,] _matrix =
    {
        //          col 0                  col 1                  col 2                col 3                 col 4               col 5
        /* row 0 */ { PhysicalKey.LeftShift, PhysicalKey.LeftControl, PhysicalKey.Escape, PhysicalKey.Q,        PhysicalKey.G,      PhysicalKey.Minus       },
        /* row 1 */ { PhysicalKey.Z,         PhysicalKey.P,           PhysicalKey.F,      PhysicalKey.Comma,    PhysicalKey.D2,     PhysicalKey.Up          },
        /* row 2 */ { PhysicalKey.Y,         PhysicalKey.O,           PhysicalKey.E,      PhysicalKey.Semicolon,PhysicalKey.D1,     PhysicalKey.Down        },
        /* row 3 */ { PhysicalKey.X,         PhysicalKey.N,           PhysicalKey.D,      PhysicalKey.Hash,     PhysicalKey.D0,     PhysicalKey.Left        },
        /* row 4 */ { PhysicalKey.W,         PhysicalKey.M,           PhysicalKey.C,      PhysicalKey.D9,       PhysicalKey.Delete, PhysicalKey.CapsLock    },
        /* row 5 */ { PhysicalKey.V,         PhysicalKey.L,           PhysicalKey.B,      PhysicalKey.D8,       PhysicalKey.F1,     PhysicalKey.Right       },
        /* row 6 */ { PhysicalKey.U,         PhysicalKey.K,           PhysicalKey.A,      PhysicalKey.D7,       PhysicalKey.Return, PhysicalKey.RightBracket},
        /* row 7 */ { PhysicalKey.T,         PhysicalKey.J,           PhysicalKey.Apostrophe, PhysicalKey.D6,   PhysicalKey.None,   PhysicalKey.Backslash   },
        /* row 8 */ { PhysicalKey.S,         PhysicalKey.I,           PhysicalKey.Slash,  PhysicalKey.D5,       PhysicalKey.None,   PhysicalKey.LeftBracket },
        /* row 9 */ { PhysicalKey.R,         PhysicalKey.H,           PhysicalKey.Period, PhysicalKey.D4,       PhysicalKey.None,   PhysicalKey.Space       },
    };

    private const int Rows = 10;
    private const int Cols = 6;

    public AtomKeyboardAdapter(IPhysicalKeyboard keyboard) => _keyboard = keyboard;

    /// <summary>
    /// Returns the Port B column byte for the row number currently driven on Port A.
    /// A bit is LOW when the key at that column in the selected row is pressed (active low).
    /// Row numbers outside 0–9 return 0xFF (no keys — open bus).
    /// </summary>
    public byte ScanColumns(byte rowSelect)
    {
        if (rowSelect >= Rows) return 0xFF;

        byte cols = 0xFF;
        for (int col = 0; col < Cols; col++)
        {
            var key = _matrix[rowSelect, col];
            if (key != PhysicalKey.None && _keyboard.IsKeyDown(key))
                cols &= (byte)~(1 << col);
        }
        return cols;
    }

    /// <summary>
    /// Returns the (row, col) matrix position for a physical key, or (-1, -1) if unmapped.
    /// Exposed for tests so assertions can use positions without embedding magic constants.
    /// </summary>
    public static (int row, int col) MatrixPosition(PhysicalKey key)
    {
        for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Cols; col++)
                if (_matrix[row, col] == key) return (row, col);
        return (-1, -1);
    }
}
