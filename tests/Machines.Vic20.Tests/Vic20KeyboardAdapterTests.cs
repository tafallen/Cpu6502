using Machines.Common;
using Machines.Vic20;

namespace Machines.Vic20.Tests;

public class Vic20KeyboardAdapterTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    // Fake keyboard: any key in the given set is "down".
    private sealed class StubKeyboard : IPhysicalKeyboard
    {
        private readonly HashSet<PhysicalKey> _down;
        public StubKeyboard(params PhysicalKey[] keys) => _down = [..keys];
        public bool IsKeyDown(PhysicalKey key) => _down.Contains(key);
    }

    // VIA 2 Port B drives columns (active low: 0 = selected).
    // ScanColumns(portB) returns the Port A row byte (active low: 0 = pressed).
    //
    // Correct hardware matrix (PRG Appendix C / jvic reference):
    //       Col0    Col1     Col2     Col3       Col4    Col5     Col6   Col7
    // Row0: 1       ←(Grv)  CTRL     RUN/STOP   SPACE   CBM      Q      2
    // Row1: 3       W        A        LSHIFT     Z       S        E      4
    // Row2: 5       R        D        X          C       F        T      6
    // Row3: 7       Y        G        V          B       H        U      8
    // Row4: 9       I        J        N          M       K        O      0
    // Row5: —       P        L        ,          .       :(Apos)  @(\)   -
    // Row6: —       —        ;        /          RSHFT   =        ↑(PgU) HOME
    // Row7: DEL(BS) RETURN   CRSR→    CRSR↓      F1      F3       F5     F7

    // ── no keys pressed ───────────────────────────────────────────────────────

    [Fact]
    public void NoKeysDown_AllColumnsSelected_Returns0xFF()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard());
        Assert.Equal(0xFF, kb.ScanColumns(0x00)); // all columns driven low
    }

    [Fact]
    public void NoKeysDown_SingleColumnSelected_Returns0xFF()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard());
        Assert.Equal(0xFF, kb.ScanColumns(0xFE)); // column 0 selected
    }

    // ── known key positions ───────────────────────────────────────────────────

    // Row 0, Col 0 → key '1'
    [Fact]
    public void Key1_Column0Selected_PullsRow0Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.D1));
        byte result = kb.ScanColumns(0xFE); // column 0 active (bit 0 = 0)
        Assert.Equal(0, result & 0x01);     // row 0 (bit 0) pulled low
    }

    // Row 0, Col 7 → key '2'
    [Fact]
    public void Key2_Column7Selected_PullsRow0Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.D2));
        byte result = kb.ScanColumns(0x7F); // column 7 active (bit 7 = 0)
        Assert.Equal(0, result & 0x01);     // row 0 (bit 0) pulled low
    }

    // Row 0, Col 6 → key 'Q'
    [Fact]
    public void KeyQ_Column6Selected_PullsRow0Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.Q));
        byte result = kb.ScanColumns(0xBF); // column 6 active (bit 6 = 0)
        Assert.Equal(0, result & 0x01);     // row 0 (bit 0) pulled low
    }

    // Row 0, Col 2 → CTRL
    [Fact]
    public void Ctrl_Column2Selected_PullsRow0Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.LeftControl));
        byte result = kb.ScanColumns(0xFB); // column 2 active (bit 2 = 0)
        Assert.Equal(0, result & 0x01);     // row 0 (bit 0)
    }

    // Row 0, Col 3 → RUN/STOP (Escape)
    [Fact]
    public void RunStop_Column3Selected_PullsRow0Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.Escape));
        byte result = kb.ScanColumns(0xF7); // column 3 active (bit 3 = 0)
        Assert.Equal(0, result & 0x01);     // row 0 (bit 0)
    }

    // Row 0, Col 4 → SPACE
    [Fact]
    public void Space_Column4Selected_PullsRow0Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.Space));
        byte result = kb.ScanColumns(0xEF); // column 4 active (bit 4 = 0)
        Assert.Equal(0, result & 0x01);     // row 0 (bit 0)
    }

    // Row 6, Col 4 → RSHIFT
    [Fact]
    public void RShift_Column4Selected_PullsRow6Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.RightShift));
        byte result = kb.ScanColumns(0xEF); // column 4 active
        Assert.Equal(0, result & 0x40);     // row 6 (bit 6) pulled low
    }

    // Row 0, Col 5 → CBM (Commodore key → LeftAlt)
    [Fact]
    public void CbmKey_Column5Selected_PullsRow0Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.LeftAlt));
        byte result = kb.ScanColumns(0xDF); // column 5 active (bit 5 = 0)
        Assert.Equal(0, result & 0x01);     // row 0 (bit 0)
    }

    // Row 7, Col 1 → RETURN
    [Fact]
    public void Return_Column1Selected_PullsRow7Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.Return));
        byte result = kb.ScanColumns(0xFD); // column 1 active
        Assert.Equal(0, result & 0x80);     // row 7 (bit 7) pulled low
    }

    // Row 7, Col 4 → F1
    [Fact]
    public void F1_Column4Selected_PullsRow7Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.F1));
        byte result = kb.ScanColumns(0xEF); // column 4 active
        Assert.Equal(0, result & 0x80);     // row 7 (bit 7) pulled low
    }

    // Row 7, Col 0 → DEL (Backspace)
    [Fact]
    public void Del_Column0Selected_PullsRow7Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.Backspace));
        byte result = kb.ScanColumns(0xFE); // column 0 active
        Assert.Equal(0, result & 0x80);     // row 7 (bit 7) pulled low
    }

    // ── column not selected ────────────────────────────────────────────────────

    [Fact]
    public void KeyDown_ButColumnNotSelected_DoesNotPullRowLow()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.D1)); // row 0, col 0
        byte result = kb.ScanColumns(0xFD); // column 1 selected, not column 0
        Assert.Equal(0x01, result & 0x01);  // row 0 stays high
    }

    // ── multiple keys in same column ──────────────────────────────────────────

    [Fact]
    public void TwoKeysInSameColumn_BothRowBitsPulledLow()
    {
        // Col 0: D1 (row 0) and D3 (row 1)
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.D1, PhysicalKey.D3));
        byte result = kb.ScanColumns(0xFE);
        Assert.Equal(0, result & 0x01); // row 0 (D1)
        Assert.Equal(0, result & 0x02); // row 1 (D3)
    }

    // ── multiple columns selected simultaneously ──────────────────────────────

    [Fact]
    public void TwoColumnsSelected_KeysInEitherColumnDetected()
    {
        // D1 is at col 0, D2 is at col 7 — select both columns simultaneously
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.D1, PhysicalKey.D2));
        byte result = kb.ScanColumns(0x7E); // columns 0 and 7 both active (bits 0 and 7 = 0)
        Assert.Equal(0, result & 0x01);     // row 0 pulled low
    }

    // ── matrix position lookup ────────────────────────────────────────────────

    [Theory]
    [InlineData(PhysicalKey.D1,            0, 0)]
    [InlineData(PhysicalKey.D2,            0, 7)]
    [InlineData(PhysicalKey.Q,             0, 6)]
    [InlineData(PhysicalKey.LeftControl,   0, 2)]
    [InlineData(PhysicalKey.Escape,        0, 3)]
    [InlineData(PhysicalKey.Space,         0, 4)]
    [InlineData(PhysicalKey.LeftAlt,       0, 5)]
    [InlineData(PhysicalKey.D3,            1, 0)]
    [InlineData(PhysicalKey.D4,            1, 7)]
    [InlineData(PhysicalKey.A,             1, 2)]
    [InlineData(PhysicalKey.LeftShift,     1, 3)]
    [InlineData(PhysicalKey.I,             4, 1)]
    [InlineData(PhysicalKey.J,             4, 2)]
    [InlineData(PhysicalKey.M,             4, 4)]
    [InlineData(PhysicalKey.D0,            4, 7)]
    [InlineData(PhysicalKey.D8,            3, 7)]
    [InlineData(PhysicalKey.RightShift,    6, 4)]
    [InlineData(PhysicalKey.Equals,        6, 5)]
    [InlineData(PhysicalKey.Home,          6, 7)]
    [InlineData(PhysicalKey.Minus,         5, 7)]
    [InlineData(PhysicalKey.Backspace,     7, 0)]
    [InlineData(PhysicalKey.Return,        7, 1)]
    [InlineData(PhysicalKey.Right,         7, 2)]
    [InlineData(PhysicalKey.Down,          7, 3)]
    [InlineData(PhysicalKey.F1,            7, 4)]
    [InlineData(PhysicalKey.F3,            7, 5)]
    [InlineData(PhysicalKey.F5,            7, 6)]
    [InlineData(PhysicalKey.F7,            7, 7)]
    public void MatrixPosition_ReturnsCorrectRowAndColumn(PhysicalKey key, int expectedRow, int expectedCol)
    {
        Assert.Equal((expectedRow, expectedCol), Vic20KeyboardAdapter.MatrixPosition(key));
    }

    [Fact]
    public void MatrixPosition_UnmappedKey_ReturnsMinusOne()
    {
        Assert.Equal((-1, -1), Vic20KeyboardAdapter.MatrixPosition(PhysicalKey.F12));
    }
}
