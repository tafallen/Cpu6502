using Machines.Vic20;

namespace Machines.Vic20.Tests;

public class Vic20TapeAdapterTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    // Two pulses: 100 cycles high, 200 cycles low (starting level is high after first edge)
    private static Vic20TapeAdapter AdapterWith(params int[] pulses)
    {
        var adapter = new Vic20TapeAdapter();
        adapter.Load(pulses);
        return adapter;
    }

    // ── initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void InitialSignalLevel_IsFalse()
    {
        var adapter = AdapterWith(100, 200);
        Assert.False(adapter.SignalLevel);
    }

    // ── motor off: no advancement ─────────────────────────────────────────────

    [Fact]
    public void MotorOff_TickDoesNotAdvance_SignalStaysLow()
    {
        var adapter = AdapterWith(100, 200);
        // Motor stays off — Tick at cycle 1000 should not produce an edge
        bool edge = adapter.Tick(1000);
        Assert.False(edge);
        Assert.False(adapter.SignalLevel);
    }

    // ── motor on: edges advance with cycles ───────────────────────────────────

    [Fact]
    public void MotorOn_TickBeforeFirstEdge_ReturnsFalse()
    {
        var adapter = AdapterWith(100, 200);
        adapter.SetMotor(true, 0);
        bool edge = adapter.Tick(50); // before first edge at cycle 100
        Assert.False(edge);
    }

    [Fact]
    public void MotorOn_TickAtFirstEdge_ReturnsTrue_AndFlipsLevel()
    {
        var adapter = AdapterWith(100, 200);
        adapter.SetMotor(true, 0);
        bool edge = adapter.Tick(100);
        Assert.True(edge);
        Assert.True(adapter.SignalLevel);
    }

    [Fact]
    public void MotorOn_TickBeyondFirstEdge_StillReturnsTrue()
    {
        var adapter = AdapterWith(100, 200);
        adapter.SetMotor(true, 0);
        bool edge = adapter.Tick(150);
        Assert.True(edge);
        Assert.True(adapter.SignalLevel);
    }

    [Fact]
    public void MotorOn_SecondEdge_FlipsLevelBack()
    {
        var adapter = AdapterWith(100, 200);
        adapter.SetMotor(true, 0);
        adapter.Tick(100); // consume first edge
        bool edge = adapter.Tick(300); // first(100) + second(200) = 300
        Assert.True(edge);
        Assert.False(adapter.SignalLevel);
    }

    // ── motor off freezes position ────────────────────────────────────────────

    [Fact]
    public void MotorOff_FreezesPosition_ResumeFromSamePoint()
    {
        // Pulse: 100 cycles high then 200 cycles low
        var adapter = AdapterWith(100, 200);
        adapter.SetMotor(true, 0);
        adapter.Tick(50);             // advance to cycle 50 — no edge yet
        adapter.SetMotor(false, 50);  // freeze at cycle 50

        // Restart motor at cycle 1000 — 50 cycles were already consumed, 50 remain
        adapter.SetMotor(true, 1000);
        bool edge = adapter.Tick(1049); // 1000 + 49: not yet
        Assert.False(edge);
        edge = adapter.Tick(1050);      // 1000 + 50: edge fires
        Assert.True(edge);
    }

    // ── OnEdge callback ───────────────────────────────────────────────────────

    [Fact]
    public void OnEdge_FiredWithNewLevel_OnEachTransition()
    {
        var levels = new List<bool>();
        var adapter = AdapterWith(100, 200);
        adapter.OnEdge = level => levels.Add(level);
        adapter.SetMotor(true, 0);

        adapter.Tick(100); // first edge
        adapter.Tick(300); // second edge

        Assert.Equal([true, false], levels);
    }

    // ── past end of tape ──────────────────────────────────────────────────────

    [Fact]
    public void PastEndOfTape_NoMoreEdges_SignalStaysAtLastLevel()
    {
        var adapter = AdapterWith(100);
        adapter.SetMotor(true, 0);
        adapter.Tick(100);   // consume only edge → level flips to true
        bool edge = adapter.Tick(9999);
        Assert.False(edge);
        Assert.True(adapter.SignalLevel);
    }
}
