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
    //
    // Source: jvic KeyboardMatrix.java keyConvMapArr, cross-referenced with
    // Commodore VIC-20 Programmer's Reference Guide Appendix C.
    //
    // Port B (VIA2 output) selects the column (bit n → col n, active-low).
    // Port A (VIA2 input) returns the row state (bit n → row n, active-low).
    //
    //       Col0        Col1         Col2         Col3         Col4         Col5         Col6         Col7
    // Row0: 1           ←(Grave)     CTRL         RUN/STOP     SPACE        CBM          Q            2
    // Row1: 3           W            A            LSHIFT       Z            S            E            4
    // Row2: 5           R            D            X            C            F            T            6
    // Row3: 7           Y            G            V            B            H            U            8
    // Row4: 9           I            J            N            M            K            O            0
    // Row5: +(none)     P            L            ,            .            :(Apostrophe) @(Backslash) -
    // Row6: £(none)     *(none)      ;            /            RSHIFT       =            ↑(PageUp)    HOME
    // Row7: DEL(BkSp)   RETURN       CRSR→        CRSR↓        F1           F3           F5           F7
    private static readonly PhysicalKey[,] Matrix = new PhysicalKey[Rows, Cols]
    {
        // Row 0
        { PhysicalKey.D1,         PhysicalKey.Grave,        PhysicalKey.LeftControl, PhysicalKey.Escape,
          PhysicalKey.Space,      PhysicalKey.LeftAlt,      PhysicalKey.Q,           PhysicalKey.D2 },

        // Row 1
        { PhysicalKey.D3,         PhysicalKey.W,            PhysicalKey.A,           PhysicalKey.LeftShift,
          PhysicalKey.Z,          PhysicalKey.S,            PhysicalKey.E,           PhysicalKey.D4 },

        // Row 2
        { PhysicalKey.D5,         PhysicalKey.R,            PhysicalKey.D,           PhysicalKey.X,
          PhysicalKey.C,          PhysicalKey.F,            PhysicalKey.T,           PhysicalKey.D6 },

        // Row 3
        { PhysicalKey.D7,         PhysicalKey.Y,            PhysicalKey.G,           PhysicalKey.V,
          PhysicalKey.B,          PhysicalKey.H,            PhysicalKey.U,           PhysicalKey.D8 },

        // Row 4
        { PhysicalKey.D9,         PhysicalKey.I,            PhysicalKey.J,           PhysicalKey.N,
          PhysicalKey.M,          PhysicalKey.K,            PhysicalKey.O,           PhysicalKey.D0 },

        // Row 5  (+ has no PC equivalent → None; : mapped to '; @ mapped to \)
        { PhysicalKey.None,       PhysicalKey.P,            PhysicalKey.L,           PhysicalKey.Comma,
          PhysicalKey.Period,     PhysicalKey.Apostrophe,   PhysicalKey.Backslash,   PhysicalKey.Minus },

        // Row 6  (£ → None; * → None; ; mapped to Semicolon; ↑ → PageUp)
        { PhysicalKey.None,       PhysicalKey.None,         PhysicalKey.Semicolon,   PhysicalKey.Slash,
          PhysicalKey.RightShift, PhysicalKey.Equals,       PhysicalKey.PageUp,      PhysicalKey.Home },

        // Row 7  (DEL/INST → Backspace; CRSR→ → Right; CRSR↓ → Down)
        { PhysicalKey.Backspace,  PhysicalKey.Return,       PhysicalKey.Right,       PhysicalKey.Down,
          PhysicalKey.F1,         PhysicalKey.F3,           PhysicalKey.F5,          PhysicalKey.F7 },
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
