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

        // Row 5  (+ via Shift+=; : mapped to '; @ mapped to \)
        { PhysicalKey.None,       PhysicalKey.P,            PhysicalKey.L,           PhysicalKey.Comma,
          PhysicalKey.Period,     PhysicalKey.Apostrophe,   PhysicalKey.Backslash,   PhysicalKey.Minus },

        // Row 6  (£ via Shift+3; * via Shift+8; ; mapped to Semicolon; ↑ → PageUp)
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
    /// <remarks>
    /// Three categories of key translation are applied:
    ///
    /// 1. Shift-to-VIC-key combos: VIC-20 keys with no PC equivalent are
    ///    reached via Shift + a base key. Both Shift and the base key are
    ///    suppressed from their normal matrix positions so the kernal sees
    ///    only the VIC combo key:
    ///      Shift+= → VIC +  (row 5, col 0)
    ///      Shift+3 → VIC £  (row 6, col 0)
    ///      Shift+8 → VIC *  (row 6, col 1)
    ///
    /// 2. Cursor-left / cursor-up: the VIC-20 has no dedicated ← or ↑ keys;
    ///    they are reached by holding Shift with the right/down cursor keys.
    ///    PC Left  → injects CRSR→ (row 7, col 2) + Shift (row 1, col 3)
    ///    PC Up    → injects CRSR↓ (row 7, col 3) + Shift (row 1, col 3)
    ///    Shift is only injected if it is not already being suppressed by a
    ///    category-1 combo.
    /// </remarks>
    public byte ScanColumns(byte portB)
    {
        bool anyShift  = _keyboard.IsKeyDown(PhysicalKey.LeftShift) ||
                         _keyboard.IsKeyDown(PhysicalKey.RightShift);

        // ── Category 1: Shift combos for VIC-only keys ────────────────────────
        bool plusCombo  = anyShift && _keyboard.IsKeyDown(PhysicalKey.Equals); // Shift+= → +
        bool poundCombo = anyShift && _keyboard.IsKeyDown(PhysicalKey.D3);     // Shift+3 → £
        bool starCombo  = anyShift && _keyboard.IsKeyDown(PhysicalKey.D8);     // Shift+8 → *

        bool suppressShift  = plusCombo || poundCombo || starCombo;
        bool suppressEquals = plusCombo;
        bool suppressD3     = poundCombo;
        bool suppressD8     = starCombo;

        // ── Category 2: cursor left / up ─────────────────────────────────────
        bool leftCursor = _keyboard.IsKeyDown(PhysicalKey.Left);
        bool upCursor   = _keyboard.IsKeyDown(PhysicalKey.Up);
        // Inject Shift for cursor combos unless a category-1 combo already
        // claimed Shift (edge case, but keeps the logic consistent).
        bool injectShiftForCursor = (leftCursor || upCursor) && !suppressShift;

        // ── Normal matrix scan ────────────────────────────────────────────────
        byte rows = 0xFF;
        for (int col = 0; col < Cols; col++)
        {
            if ((portB & (1 << col)) != 0) continue; // column not driven
            for (int row = 0; row < Rows; row++)
            {
                var key = Matrix[row, col];
                if (key == PhysicalKey.None) continue;
                if (suppressShift  && (key == PhysicalKey.LeftShift  || key == PhysicalKey.RightShift)) continue;
                if (suppressEquals && key == PhysicalKey.Equals) continue;
                if (suppressD3     && key == PhysicalKey.D3)     continue;
                if (suppressD8     && key == PhysicalKey.D8)     continue;
                if (_keyboard.IsKeyDown(key))
                    rows &= (byte)~(1 << row);
            }
        }

        // ── Inject virtual keys ───────────────────────────────────────────────
        // Category 1: + [row5,col0], £ [row6,col0], * [row6,col1]
        if (plusCombo  && (portB & (1 << 0)) == 0) rows &= unchecked((byte)~(1 << 5));
        if (poundCombo && (portB & (1 << 0)) == 0) rows &= unchecked((byte)~(1 << 6));
        if (starCombo  && (portB & (1 << 1)) == 0) rows &= unchecked((byte)~(1 << 6));

        // Category 2: CRSR→ [row7,col2], CRSR↓ [row7,col3], Shift [row1,col3]
        if (leftCursor && (portB & (1 << 2)) == 0) rows &= unchecked((byte)~(1 << 7));
        if (upCursor   && (portB & (1 << 3)) == 0) rows &= unchecked((byte)~(1 << 7));
        if (injectShiftForCursor && (portB & (1 << 3)) == 0) rows &= unchecked((byte)~(1 << 1));

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
