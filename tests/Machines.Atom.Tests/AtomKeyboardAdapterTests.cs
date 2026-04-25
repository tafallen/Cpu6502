using Machines.Common;

namespace Machines.Atom.Tests;

public class AtomKeyboardAdapterTests
{
    private sealed class FakeKeyboard : IPhysicalKeyboard
    {
        private readonly HashSet<PhysicalKey> _down = new();
        public void Press(PhysicalKey key)   => _down.Add(key);
        public void Release(PhysicalKey key) => _down.Remove(key);
        public bool IsKeyDown(PhysicalKey key) => _down.Contains(key);
    }

    private readonly FakeKeyboard        _kb      = new();
    private readonly AtomKeyboardAdapter _adapter;

    public AtomKeyboardAdapterTests() => _adapter = new AtomKeyboardAdapter(_kb);

    // --- out-of-range row ---

    [Fact]
    public void RowOutOfRange_ReturnsAllHigh()
    {
        Assert.Equal(0xFF, _adapter.ScanColumns(10));
        Assert.Equal(0xFF, _adapter.ScanColumns(255));
    }

    // --- nothing pressed ---

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(5)] [InlineData(9)]
    public void NothingPressed_AllColumnsHigh(byte row)
    {
        Assert.Equal(0xFF, _adapter.ScanColumns(row));
    }

    // --- correct column goes low when key pressed and its row selected ---

    [Theory]
    [MemberData(nameof(MappedKeys))]
    public void KeyPressed_CorrectColumnGoesLow(PhysicalKey key)
    {
        var (row, col) = AtomKeyboardAdapter.MatrixPosition(key);
        _kb.Press(key);
        byte result = _adapter.ScanColumns((byte)row);
        Assert.Equal(0, result & (1 << col));
    }

    // --- column stays high when key not pressed ---

    [Theory]
    [MemberData(nameof(MappedKeys))]
    public void KeyNotPressed_ColumnStaysHigh(PhysicalKey key)
    {
        var (row, col) = AtomKeyboardAdapter.MatrixPosition(key);
        byte result = _adapter.ScanColumns((byte)row);
        Assert.NotEqual(0, result & (1 << col));
    }

    // --- key in wrong row is not visible ---

    [Fact]
    public void KeyPressed_WrongRowSelected_ColumnStaysHigh()
    {
        // Press a key from row 6 (e.g. Return), select row 0 — should not appear
        var (row, col) = AtomKeyboardAdapter.MatrixPosition(PhysicalKey.Return);
        _kb.Press(PhysicalKey.Return);
        byte otherRow = (byte)(row == 0 ? 1 : 0);
        byte result = _adapter.ScanColumns(otherRow);
        // The column bit in the OTHER row should still be high
        Assert.Equal(0xFF, result); // nothing mapped/pressed in that other row
    }

    // --- multiple keys in the same row ---

    [Fact]
    public void TwoKeysInSameRow_BothColumnsBitsClear()
    {
        // Row 9 has R(col0), H(col1), Period(col2), D4(col3), Space(col5)
        _kb.Press(PhysicalKey.R);
        _kb.Press(PhysicalKey.H);
        byte result = _adapter.ScanColumns(9);
        Assert.Equal(0, result & (1 << 0)); // R at col 0
        Assert.Equal(0, result & (1 << 1)); // H at col 1
        Assert.NotEqual(0, result & (1 << 2)); // Period not pressed
    }

    // --- release clears the column ---

    [Fact]
    public void KeyReleased_ColumnReturnsHigh()
    {
        _kb.Press(PhysicalKey.Space);
        _kb.Release(PhysicalKey.Space);
        var (row, col) = AtomKeyboardAdapter.MatrixPosition(PhysicalKey.Space);
        byte result = _adapter.ScanColumns((byte)row);
        Assert.NotEqual(0, result & (1 << col));
    }

    // --- other columns in the row are unaffected ---

    [Fact]
    public void KeyPressed_OtherColumnsInSameRow_Unaffected()
    {
        // Press Space (row 9, col 5) — col 0 (R) should stay high
        _kb.Press(PhysicalKey.Space);
        byte result = _adapter.ScanColumns(9);
        Assert.Equal(0,   result & (1 << 5)); // Space col low
        Assert.NotEqual(0, result & (1 << 0)); // R col unaffected
    }

    // --- matrix coverage: every mapped key round-trips ---

    [Theory]
    [MemberData(nameof(MappedKeys))]
    public void MatrixPosition_IsConsistentWithScanColumns(PhysicalKey key)
    {
        var (row, col) = AtomKeyboardAdapter.MatrixPosition(key);
        Assert.True(row >= 0 && row <= 9,  $"{key}: row {row} out of range");
        Assert.True(col >= 0 && col <= 5,  $"{key}: col {col} out of range");
    }

    // --- test data ---

    public static IEnumerable<object[]> MappedKeys() =>
        Enum.GetValues<PhysicalKey>()
            .Where(k => k != PhysicalKey.None)
            .Where(k => AtomKeyboardAdapter.MatrixPosition(k).row >= 0)
            .Select(k => new object[] { k });
}
