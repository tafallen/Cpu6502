using Machines.Common;

namespace Machines.Atom.Tests;

public class AtomSoundAdapterTests
{
    private sealed class CaptureSink : IAudioSink
    {
        public short[]? Samples;
        public int SampleRate;
        public void SubmitSamples(ReadOnlySpan<short> samples, int sampleRate)
        {
            Samples = samples.ToArray();
            SampleRate = sampleRate;
        }
    }

    private readonly CaptureSink        _sink    = new();
    private readonly AtomSoundAdapter   _adapter;

    public AtomSoundAdapterTests() => _adapter = new AtomSoundAdapter(_sink);

    // ── helpers ───────────────────────────────────────────────────────────────

    // PC4 is bit 4 of Port C.
    private static byte WithPc4(bool high) => high ? (byte)0x10 : (byte)0x00;

    private void RunFrame(ulong startCycle, ulong endCycle, Action? midFrame = null)
    {
        _adapter.BeginFrame(startCycle);
        midFrame?.Invoke();
        _adapter.EndFrame(endCycle);
    }

    // ── sample count and rate ─────────────────────────────────────────────────

    [Fact]
    public void EndFrame_Submits_882_Samples_At_44100Hz()
    {
        RunFrame(0, AtomSoundAdapter.CyclesPerFrame);
        Assert.Equal(AtomSoundAdapter.SamplesPerFrame, _sink.Samples!.Length);
        Assert.Equal(AtomSoundAdapter.SampleRate, _sink.SampleRate);
    }

    // ── default state: PC4 low, no toggles ───────────────────────────────────

    [Fact]
    public void DefaultState_AllSamplesAreLow()
    {
        RunFrame(0, AtomSoundAdapter.CyclesPerFrame);
        Assert.All(_sink.Samples!, s => Assert.Equal(AtomSoundAdapter.SampleLow, s));
    }

    // ── PC4 held high, no toggles ─────────────────────────────────────────────

    [Fact]
    public void PC4High_BeforeFrame_AllSamplesAreHigh()
    {
        // Drive PC4 high before the frame begins
        _adapter.NotifyPortC(WithPc4(true), 0);
        RunFrame(1, 1 + AtomSoundAdapter.CyclesPerFrame);
        Assert.All(_sink.Samples!, s => Assert.Equal(AtomSoundAdapter.SampleHigh, s));
    }

    // ── single toggle at frame midpoint ──────────────────────────────────────

    [Fact]
    public void ToggleAtMidpoint_FirstHalfLow_SecondHalfHigh()
    {
        ulong half = AtomSoundAdapter.CyclesPerFrame / 2; // 10000

        _adapter.BeginFrame(0);
        _adapter.NotifyPortC(WithPc4(true), half); // toggle to high at mid-frame
        _adapter.EndFrame(AtomSoundAdapter.CyclesPerFrame);

        int mid = AtomSoundAdapter.SamplesPerFrame / 2; // 441
        for (int i = 0;   i < mid; i++) Assert.Equal(AtomSoundAdapter.SampleLow,  _sink.Samples![i]);
        for (int i = mid; i < AtomSoundAdapter.SamplesPerFrame; i++) Assert.Equal(AtomSoundAdapter.SampleHigh, _sink.Samples![i]);
    }

    // ── toggle at very start of frame ────────────────────────────────────────

    [Fact]
    public void ToggleAtFrameStart_AllSamplesHigh()
    {
        _adapter.BeginFrame(0);
        _adapter.NotifyPortC(WithPc4(true), 0); // toggle at exact start
        _adapter.EndFrame(AtomSoundAdapter.CyclesPerFrame);
        Assert.All(_sink.Samples!, s => Assert.Equal(AtomSoundAdapter.SampleHigh, s));
    }

    // ── toggle at very end of frame ───────────────────────────────────────────

    [Fact]
    public void ToggleAtFrameEnd_AllButLastSampleLow()
    {
        ulong end = AtomSoundAdapter.CyclesPerFrame;
        _adapter.BeginFrame(0);
        _adapter.NotifyPortC(WithPc4(true), end); // toggle at last cycle
        _adapter.EndFrame(end);
        // All samples map to cycles < end, so they should still see Low
        Assert.All(_sink.Samples!, s => Assert.Equal(AtomSoundAdapter.SampleLow, s));
    }

    // ── no duplicate toggles from same Port C value ───────────────────────────

    [Fact]
    public void RepeatedSameState_DoesNotToggle()
    {
        _adapter.BeginFrame(0);
        _adapter.NotifyPortC(WithPc4(false), 100);  // PC4 already low — no change
        _adapter.NotifyPortC(WithPc4(false), 200);
        _adapter.EndFrame(AtomSoundAdapter.CyclesPerFrame);
        Assert.All(_sink.Samples!, s => Assert.Equal(AtomSoundAdapter.SampleLow, s));
    }

    // ── toggles outside the frame are still tracked for state ─────────────────

    [Fact]
    public void ToggleBeforeFrame_StateCarriesIntoFrame()
    {
        // Toggle PC4 high before BeginFrame is called
        _adapter.NotifyPortC(WithPc4(true), 0);
        // Then run a frame with no further toggles
        RunFrame(100, 100 + AtomSoundAdapter.CyclesPerFrame);
        Assert.All(_sink.Samples!, s => Assert.Equal(AtomSoundAdapter.SampleHigh, s));
    }

    // ── multiple consecutive frames ───────────────────────────────────────────

    [Fact]
    public void MultipleFrames_EachReceives882Samples()
    {
        int callCount = 0;
        var countingSink = new CountingSink(count => callCount = count);
        var adapter = new AtomSoundAdapter(countingSink);

        for (int frame = 0; frame < 3; frame++)
        {
            ulong start = (ulong)(frame * AtomSoundAdapter.CyclesPerFrame);
            adapter.BeginFrame(start);
            adapter.EndFrame(start + AtomSoundAdapter.CyclesPerFrame);
        }

        Assert.Equal(3, callCount);
    }

    private sealed class CountingSink(Action<int> onSubmit) : IAudioSink
    {
        private int _calls;
        public void SubmitSamples(ReadOnlySpan<short> samples, int sampleRate)
            => onSubmit(++_calls);
    }

    // ── square wave: two toggles per frame produce alternating blocks ─────────

    [Fact]
    public void TwoTogglesPerFrame_ProducesThreeBlocks()
    {
        ulong third = AtomSoundAdapter.CyclesPerFrame / 3;

        _adapter.BeginFrame(0);
        _adapter.NotifyPortC(WithPc4(true),  third);      // Low→High at 1/3
        _adapter.NotifyPortC(WithPc4(false), third * 2);  // High→Low at 2/3
        _adapter.EndFrame(AtomSoundAdapter.CyclesPerFrame);

        int s1 = AtomSoundAdapter.SamplesPerFrame / 3;
        int s2 = s1 * 2;

        for (int i = 0;  i < s1; i++) Assert.Equal(AtomSoundAdapter.SampleLow,  _sink.Samples![i]);
        for (int i = s1; i < s2; i++) Assert.Equal(AtomSoundAdapter.SampleHigh, _sink.Samples![i]);
        for (int i = s2; i < AtomSoundAdapter.SamplesPerFrame; i++) Assert.Equal(AtomSoundAdapter.SampleLow, _sink.Samples![i]);
    }
}
