namespace Machines.Atom.Tests;

public class AtomTapeAdapterTests
{
    // 300 baud, 1 MHz clock: each bit lasts 3333 cycles.
    private const ulong CyclesPerBit = AtomTapeAdapter.CyclesPerBit;

    // ── construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NoTape_ReadBitReturnsHigh()
    {
        var tape = new AtomTapeAdapter();
        Assert.True(tape.ReadBit(0));
    }

    [Fact]
    public void LoadBits_ThenReadBit_ReturnsFirstBit()
    {
        var tape = new AtomTapeAdapter();
        tape.Load([false, true, true]);
        Assert.False(tape.ReadBit(0));
    }

    // ── motor control ─────────────────────────────────────────────────────────

    [Fact]
    public void MotorOff_HoldsPosition()
    {
        var tape = new AtomTapeAdapter();
        tape.Load([false, true]);
        tape.SetMotor(false);
        // Advance many cycles — position must not change
        tape.ReadBit(CyclesPerBit * 10);
        Assert.False(tape.ReadBit(CyclesPerBit * 10)); // still on bit 0
    }

    [Fact]
    public void MotorOn_AdvancesPosition()
    {
        var tape = new AtomTapeAdapter();
        tape.Load([false, true, false]);
        tape.SetMotor(true);
        // After one full bit period the read head should be on bit 1
        bool bit1 = tape.ReadBit(CyclesPerBit);
        Assert.True(bit1);
    }

    // ── bit streaming ─────────────────────────────────────────────────────────

    [Fact]
    public void ReadBit_ReturnsCorrectBitsInSequence()
    {
        // Three bits: false, true, false
        var tape = new AtomTapeAdapter();
        tape.Load([false, true, false]);
        tape.SetMotor(true);

        Assert.False(tape.ReadBit(0));
        Assert.True(tape.ReadBit(CyclesPerBit));
        Assert.False(tape.ReadBit(CyclesPerBit * 2));
    }

    [Fact]
    public void ReadBit_PastEnd_ReturnsHigh()
    {
        var tape = new AtomTapeAdapter();
        tape.Load([false]);
        tape.SetMotor(true);
        // Read past end of tape
        Assert.True(tape.ReadBit(CyclesPerBit * 5));
    }

    [Fact]
    public void ReadBit_WithinSameBitPeriod_ReturnsSameBit()
    {
        var tape = new AtomTapeAdapter();
        tape.Load([false, true]);
        tape.SetMotor(true);

        // Both reads within first bit period (0 .. CyclesPerBit-1) → false
        Assert.False(tape.ReadBit(0));
        Assert.False(tape.ReadBit(CyclesPerBit - 1));
    }

    // ── reload ────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_Rewinds_ToStart()
    {
        var tape = new AtomTapeAdapter();
        tape.Load([false, true]);
        tape.SetMotor(true);
        // Advance past bit 0
        tape.ReadBit(CyclesPerBit);
        // Reload different data — should start from bit 0 again
        tape.Load([true, false]);
        Assert.True(tape.ReadBit(0));
    }

    // ── UEF integration ───────────────────────────────────────────────────────

    [Fact]
    public void LoadUef_ParsesAndLoads()
    {
        using var ms = new System.IO.MemoryStream();
        using (var w = new System.IO.BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("UEF File!"));
            w.Write((byte)0);  // null terminator
            w.Write((byte)10); // minor
            w.Write((byte)0);  // major
            // Chunk 0x0100 with one byte 0x00 → 10 bits
            w.Write((ushort)0x0100);
            w.Write((int)1);
            w.Write((byte)0x00);
        }
        ms.Seek(0, System.IO.SeekOrigin.Begin);

        var tape = new AtomTapeAdapter();
        tape.LoadUef(ms);
        tape.SetMotor(true);

        Assert.False(tape.ReadBit(0)); // start bit
    }
}
