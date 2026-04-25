using Machines.Common;

namespace Machines.Atom;

/// <summary>
/// Synthesises audio from Acorn Atom PPI Port C bit 4 (PC4) toggles.
///
/// The Atom has no dedicated sound chip.  The CPU bit-bangs PC4 at audio
/// frequency to produce a square wave.  This adapter records the CPU cycle
/// at each PC4 transition, then at the end of each video frame interpolates
/// those transitions into a PCM sample buffer and pushes it to an IAudioSink.
///
/// Call order per frame:
///   BeginFrame(startCycle)
///   ... CPU steps; NotifyPortC called whenever Port C latch changes ...
///   EndFrame(endCycle)
/// </summary>
public sealed class AtomSoundAdapter
{
    public const int SampleRate     = 44100;
    public const int FrameRate      = 50;
    public const int SamplesPerFrame = SampleRate / FrameRate;   // 882
    public const int CyclesPerFrame  = 1_000_000 / FrameRate;   // 20 000  (1 MHz CPU)

    public const short SampleHigh =  16000;
    public const short SampleLow  = -16000;

    private readonly IAudioSink _sink;
    private readonly List<(ulong Cycle, bool High)> _toggles = new();

    private bool  _pc4High;           // current PC4 state (updated every NotifyPortC)
    private bool  _pc4AtFrameStart;   // PC4 state captured by BeginFrame
    private ulong _frameStartCycle;

    public AtomSoundAdapter(IAudioSink sink) => _sink = sink;

    /// <summary>
    /// Mark the start of a new video frame. Captures the current PC4 state
    /// and clears the toggle list from the previous frame.
    /// </summary>
    public void BeginFrame(ulong startCycle)
    {
        _frameStartCycle  = startCycle;
        _pc4AtFrameStart  = _pc4High;
        _toggles.Clear();
    }

    /// <summary>
    /// Call whenever PPI Port C latch changes. Records PC4 transitions with
    /// their exact CPU cycle count for sample-accurate interpolation.
    /// </summary>
    public void NotifyPortC(byte portCValue, ulong cycleCount)
    {
        bool high = (portCValue & 0x10) != 0;
        if (high == _pc4High) return; // no change — ignore
        _pc4High = high;
        _toggles.Add((cycleCount, high));
    }

    /// <summary>
    /// Synthesise the frame's audio samples from the recorded toggle history
    /// and submit them to the IAudioSink.
    /// </summary>
    public void EndFrame(ulong endCycle)
    {
        var samples = new short[SamplesPerFrame];
        ulong frameLen = endCycle > _frameStartCycle
            ? endCycle - _frameStartCycle
            : 1;

        bool  state = _pc4AtFrameStart;
        int   ti    = 0;

        for (int i = 0; i < SamplesPerFrame; i++)
        {
            ulong sampleCycle = _frameStartCycle
                + (ulong)((long)i * (long)frameLen / SamplesPerFrame);

            while (ti < _toggles.Count && _toggles[ti].Cycle <= sampleCycle)
                state = _toggles[ti++].High;

            samples[i] = state ? SampleHigh : SampleLow;
        }

        _sink.SubmitSamples(samples, SampleRate);
    }
}
