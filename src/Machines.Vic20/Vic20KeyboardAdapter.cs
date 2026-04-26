using Machines.Common;

namespace Machines.Vic20;

/// <summary>
/// Maps physical keyboard state to the VIC-20 8×8 key matrix.
///
/// VIA 2 scans the keyboard:
///   Port B (output, active low) — drives one or more columns to 0
///   Port A (input, active low)  — returns which rows have a pressed key
///
/// The OS writes a column mask to Port B and reads Port A. A bit that is 0
/// in the Port A result means the key at that (row, column) intersection is pressed.
///
/// Matrix source: Commodore VIC-20 Programmer's Reference Guide, Appendix C.
/// </summary>
public sealed class Vic20KeyboardAdapter
{
    private const int Rows = 8;
    private const int Cols = 8;

    private readonly IPhysicalKeyboard _keyboard;

    // [row, col] → physical key
    private static readonly PhysicalKey[,] Matrix = new PhysicalKey[Rows, Cols]
    {
        // Row 0: number keys 1–8
        { PhysicalKey.D1, PhysicalKey.D2, PhysicalKey.D3, PhysicalKey.D4,
          PhysicalKey.D5, PhysicalKey.D6, PhysicalKey.D7, PhysicalKey.D8 },

        // Row 1: Q–I
        { PhysicalKey.Q, PhysicalKey.W, PhysicalKey.E, PhysicalKey.R,
          PhysicalKey.T, PhysicalKey.Y, PhysicalKey.U, PhysicalKey.I },

        // Row 2: CTRL, A–J
        { PhysicalKey.LeftControl, PhysicalKey.A, PhysicalKey.S, PhysicalKey.D,
          PhysicalKey.F, PhysicalKey.G, PhysicalKey.H, PhysicalKey.J },

        // Row 3: RUN/STOP (Escape), Z–M
        { PhysicalKey.Escape, PhysicalKey.Z, PhysicalKey.X, PhysicalKey.C,
          PhysicalKey.V, PhysicalKey.B, PhysicalKey.N, PhysicalKey.M },

        // Row 4: SPACE, CRSR←→ (Right), @([), *( ]), / , CRSR↓↑ (Down), =, RSHIFT
        { PhysicalKey.Space,      PhysicalKey.Right,       PhysicalKey.LeftBracket,
          PhysicalKey.RightBracket, PhysicalKey.Slash,     PhysicalKey.Down,
          PhysicalKey.Equals,     PhysicalKey.RightShift },

        // Row 5: CBM (LeftAlt), ;, :  (Apostrophe), . , , , 0, 9, -
        { PhysicalKey.LeftAlt,    PhysicalKey.Semicolon,   PhysicalKey.Apostrophe,
          PhysicalKey.Period,     PhysicalKey.Comma,       PhysicalKey.D0,
          PhysicalKey.D9,         PhysicalKey.Minus },

        // Row 6: ← arrow (Delete), ↑ arrow (Grave), +  (Hash), Home, L, P, O, K
        { PhysicalKey.Delete,     PhysicalKey.Grave,       PhysicalKey.Hash,
          PhysicalKey.Home,       PhysicalKey.L,           PhysicalKey.P,
          PhysicalKey.O,          PhysicalKey.K },

        // Row 7: CRSR↓↑ (Up), F1, F3, F5, F7, End, DEL (Backspace), RETURN
        { PhysicalKey.Up,         PhysicalKey.F1,          PhysicalKey.F3,
          PhysicalKey.F5,         PhysicalKey.F7,          PhysicalKey.End,
          PhysicalKey.Backspace,  PhysicalKey.Return },
    };

    // Reverse lookup: physical key → (row, col)
    private static readonly Dictionary<PhysicalKey, (int Row, int Col)> _reverseMap = BuildReverseMap();

    public Vic20KeyboardAdapter(IPhysicalKeyboard keyboard) => _keyboard = keyboard;

    /// <summary>
    /// Returns the Port A row byte for the given Port B column selection.
    /// Bits that are 0 in <paramref name="portB"/> indicate active columns.
    /// Bits that are 0 in the return value indicate pressed keys in that row.
    /// </summary>
    public byte ScanColumns(byte portB)
    {
        byte rows = 0xFF;
        for (int col = 0; col < Cols; col++)
        {
            if ((portB & (1 << col)) != 0) continue; // column not driven
            for (int row = 0; row < Rows; row++)
            {
                var key = Matrix[row, col];
                if (key != PhysicalKey.None && _keyboard.IsKeyDown(key))
                    rows &= (byte)~(1 << row);
            }
        }
        return rows;
    }

    /// <summary>Returns the (row, column) for a physical key, or (-1,-1) if unmapped.</summary>
    public static (int Row, int Col) MatrixPosition(PhysicalKey key) =>
        _reverseMap.TryGetValue(key, out var pos) ? pos : (-1, -1);

    private static Dictionary<PhysicalKey, (int, int)> BuildReverseMap()
    {
        var map = new Dictionary<PhysicalKey, (int, int)>();
        for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Cols; col++)
            {
                var key = Matrix[row, col];
                if (key != PhysicalKey.None)
                    map[key] = (row, col);
            }
        return map;
    }
}
