namespace Machines.Atom;

/// <summary>
/// Streams UEF tape bits to the CPU at 300 baud (3333 cycles/bit at 1 MHz).
/// The motor must be on for the position to advance.
/// ReadBit(cycleCount) returns the current bit level for the given absolute cycle count.
/// </summary>
public sealed class AtomTapeAdapter
{
    public const ulong CyclesPerBit = 3333; // 1 MHz / 300 baud

    private bool[]  _bits     = [];
    private bool    _motorOn  = false;
    private ulong   _motorOnAt = 0;
    private int     _posAtMotorOn = 0;

    public void Load(bool[] bits)
    {
        _bits         = bits;
        _motorOn      = false;
        _motorOnAt    = 0;
        _posAtMotorOn = 0;
    }

    public void LoadUef(Stream stream) => Load(UefParser.Parse(stream).ToArray());

    public void SetMotor(bool on)
    {
        if (on == _motorOn) return;
        if (on)
        {
            // remember cycle at which motor started and position at that point
            // caller hasn't told us the cycle yet — store a sentinel; position
            // is resolved on next ReadBit
            _motorOn = true;
        }
        else
        {
            _motorOn = false;
        }
    }

    /// <summary>
    /// Returns the bit at the current tape position for the given CPU cycle count.
    /// When the motor is off, position does not advance.
    /// </summary>
    public bool ReadBit(ulong currentCycle)
    {
        if (!_motorOn) return GetBitAt(_posAtMotorOn);

        // First call after motor-on: anchor the start cycle
        if (_motorOnAt == 0 && currentCycle == 0)
        {
            // position stays at _posAtMotorOn
        }

        ulong elapsed = currentCycle >= _motorOnAt
            ? currentCycle - _motorOnAt
            : 0;

        int pos = _posAtMotorOn + (int)(elapsed / CyclesPerBit);
        return GetBitAt(pos);
    }

    private bool GetBitAt(int pos) =>
        (pos < _bits.Length) ? _bits[pos] : true;

    // ── Motor-on cycle anchoring ──────────────────────────────────────────────

    /// <summary>
    /// Call when turning the motor on, supplying the current CPU cycle count.
    /// AtomMachine calls this when PC5 transitions to 1.
    /// </summary>
    public void MotorOn(ulong currentCycle)
    {
        if (_motorOn) return;
        _motorOn      = true;
        _motorOnAt    = currentCycle;
    }

    /// <summary>
    /// Call when turning the motor off, supplying the current CPU cycle count,
    /// so the tape position is frozen correctly.
    /// </summary>
    public void MotorOff(ulong currentCycle)
    {
        if (!_motorOn) return;
        ulong elapsed  = currentCycle - _motorOnAt;
        _posAtMotorOn += (int)(elapsed / CyclesPerBit);
        _motorOn       = false;
        _motorOnAt     = 0;
    }
}
