namespace Machines.Vic20;

/// <summary>
/// First-order single-pole IIR low-pass filter (4 kHz cutoff frequency)
/// for smoothing square wave digital audio synthesis and eliminating high-frequency aliasing clicks.
/// </summary>
public sealed class AudioLowPassFilter
{
    private readonly float _alpha;
    private float _lastOutput;

    public AudioLowPassFilter(float sampleRate = 44100f, float cutoffHz = 4000f)
    {
        float dt = 1.0f / sampleRate;
        float rc = 1.0f / (2.0f * MathF.PI * cutoffHz);
        _alpha = dt / (rc + dt);
    }

    public short Process(short sample)
    {
        _lastOutput = _lastOutput + _alpha * (sample - _lastOutput);
        return (short)MathF.Min(MathF.Max(_lastOutput, short.MinValue), short.MaxValue);
    }

    public void ProcessBuffer(short[] samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Process(samples[i]);
        }
    }
}
