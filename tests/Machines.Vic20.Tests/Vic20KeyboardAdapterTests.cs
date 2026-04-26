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

    // Row 0, Col 1 → key '2'
    [Fact]
    public void Key2_Column1Selected_PullsRow0Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.D2));
        byte result = kb.ScanColumns(0xFD); // column 1 active
        Assert.Equal(0, result & 0x01);
    }

    // Row 1, Col 0 → key 'Q'
    [Fact]
    public void KeyQ_Column0Selected_PullsRow1Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.Q));
        byte result = kb.ScanColumns(0xFE);
        Assert.Equal(0, result & 0x02); // row 1 (bit 1) pulled low
    }

    // Row 2, Col 0 → CTRL
    [Fact]
    public void Ctrl_Column0Selected_PullsRow2Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.LeftControl));
        byte result = kb.ScanColumns(0xFE);
        Assert.Equal(0, result & 0x04); // row 2 (bit 2)
    }

    // Row 3, Col 0 → RUN/STOP (Escape)
    [Fact]
    public void RunStop_Column0Selected_PullsRow3Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.Escape));
        byte result = kb.ScanColumns(0xFE);
        Assert.Equal(0, result & 0x08); // row 3 (bit 3)
    }

    // Row 4, Col 0 → SPACE
    [Fact]
    public void Space_Column0Selected_PullsRow4Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.Space));
        byte result = kb.ScanColumns(0xFE);
        Assert.Equal(0, result & 0x10); // row 4 (bit 4)
    }

    // Row 4, Col 7 → RSHIFT
    [Fact]
    public void RShift_Column7Selected_PullsRow4Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.RightShift));
        byte result = kb.ScanColumns(0x7F); // column 7 active (bit 7 = 0)
        Assert.Equal(0, result & 0x10);
    }

    // Row 5, Col 0 → CBM (Commodore key → LeftAlt)
    [Fact]
    public void CbmKey_Column0Selected_PullsRow5Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.LeftAlt));
        byte result = kb.ScanColumns(0xFE);
        Assert.Equal(0, result & 0x20); // row 5 (bit 5)
    }

    // Row 7, Col 7 → RETURN
    [Fact]
    public void Return_Column7Selected_PullsRow7Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.Return));
        byte result = kb.ScanColumns(0x7F);
        Assert.Equal(0, result & 0x80); // row 7 (bit 7)
    }

    // Row 7, Col 1 → F1
    [Fact]
    public void F1_Column1Selected_PullsRow7Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.F1));
        byte result = kb.ScanColumns(0xFD);
        Assert.Equal(0, result & 0x80);
    }

    // Row 7, Col 6 → DEL (Backspace)
    [Fact]
    public void Del_Column6Selected_PullsRow7Low()
    {
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.Backspace));
        byte result = kb.ScanColumns(0xBF); // column 6 active
        Assert.Equal(0, result & 0x80);
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
        // Col 0: D1 (row 0) and Q (row 1)
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.D1, PhysicalKey.Q));
        byte result = kb.ScanColumns(0xFE);
        Assert.Equal(0, result & 0x01); // row 0
        Assert.Equal(0, result & 0x02); // row 1
    }

    // ── multiple columns selected simultaneously ──────────────────────────────

    [Fact]
    public void TwoColumnsSelected_KeysInEitherColumnDetected()
    {
        // D1 is at col 0, D2 is at col 1 — select both columns simultaneously
        var kb = new Vic20KeyboardAdapter(new StubKeyboard(PhysicalKey.D1, PhysicalKey.D2));
        byte result = kb.ScanColumns(0xFC); // columns 0 and 1 both active
        Assert.Equal(0, result & 0x01);     // row 0 pulled low
    }

    // ── matrix position lookup ────────────────────────────────────────────────

    [Theory]
    [InlineData(PhysicalKey.D1,            0, 0)]
    [InlineData(PhysicalKey.D8,            0, 7)]
    [InlineData(PhysicalKey.Q,             1, 0)]
    [InlineData(PhysicalKey.I,             1, 7)]
    [InlineData(PhysicalKey.LeftControl,   2, 0)]
    [InlineData(PhysicalKey.J,             2, 7)]
    [InlineData(PhysicalKey.Escape,        3, 0)]
    [InlineData(PhysicalKey.M,             3, 7)]
    [InlineData(PhysicalKey.Space,         4, 0)]
    [InlineData(PhysicalKey.RightShift,    4, 7)]
    [InlineData(PhysicalKey.LeftAlt,       5, 0)]
    [InlineData(PhysicalKey.Minus,         5, 7)]
    [InlineData(PhysicalKey.Return,        7, 7)]
    [InlineData(PhysicalKey.F1,            7, 1)]
    [InlineData(PhysicalKey.F3,            7, 2)]
    [InlineData(PhysicalKey.F5,            7, 3)]
    [InlineData(PhysicalKey.F7,            7, 4)]
    [InlineData(PhysicalKey.Backspace,     7, 6)]
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
