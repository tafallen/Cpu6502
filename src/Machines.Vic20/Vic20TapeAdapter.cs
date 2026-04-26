namespace Machines.Vic20;

/// <summary>
/// Streams Commodore TAP pulse edges to the CPU.
/// The motor must be on for the tape position to advance.
/// Each pulse defines how long the current level holds before the next transition.
/// </summary>
public sealed class Vic20TapeAdapter
{
    private int[]  _pulses      = [];
    private int    _index       = 0;       // index of current (active) pulse
    private ulong  _pulseStart  = 0;       // absolute cycle when the current pulse began
    private bool   _motorOn     = false;
    private ulong  _motorOnAt   = 0;
    private ulong  _pausedOffset = 0;      // cycles already consumed in the current pulse when motor turned off

    public bool SignalLevel { get; private set; }

    /// <summary>Fired on each level transition with the new signal level.</summary>
    public Action<bool>? OnEdge { get; set; }

    public void Load(int[] pulses)
    {
        _pulses       = pulses;
        _index        = 0;
        _motorOn      = false;
        _motorOnAt    = 0;
        _pausedOffset = 0;
        _pulseStart   = 0;
        SignalLevel   = false;
    }

    public void LoadTap(Stream stream) => Load(TapParser.Parse(stream));

    /// <summary>
    /// Cycle-accurate motor control.
    /// Call when the motor-relay bit changes, passing the current CPU cycle count.
    /// </summary>
    public void SetMotor(bool on, ulong currentCycle)
    {
        if (on == _motorOn) return;

        if (on)
        {
            _motorOn    = true;
            _motorOnAt  = currentCycle;
            // pulseStart is set such that (currentCycle - _pulseStart) == _pausedOffset
            _pulseStart = currentCycle - _pausedOffset;
        }
        else
        {
            // Freeze: record how far through the current pulse we are
            _pausedOffset = currentCycle - _pulseStart;
            _motorOn = false;
        }
    }

    /// <summary>
    /// Advance tape state to <paramref name="currentCycle"/>.
    /// Returns true (and updates SignalLevel) if an edge occurred since last call.
    /// </summary>
    public bool Tick(ulong currentCycle)
    {
        if (!_motorOn || _index >= _pulses.Length) return false;

        ulong pulseEnd = _pulseStart + (ulong)_pulses[_index];
        if (currentCycle < pulseEnd) return false;

        // Edge fires
        SignalLevel = !SignalLevel;
        OnEdge?.Invoke(SignalLevel);
        _index++;
        _pulseStart = pulseEnd;
        _pausedOffset = 0;

        return true;
    }
}
