using Cpu6502.Core;

namespace Machines.Vic20;

/// <summary>
/// MOS 6522 Versatile Interface Adapter.
/// Implements two bidirectional 8-bit ports, two 16-bit interval timers,
/// and an interrupt controller. Timer 1 supports free-run mode; Timer 2 is one-shot only.
/// The shift register is not implemented (VIC-20 does not use it for core operation).
/// </summary>
public sealed class Via6522 : IBus
{
    // ── Port I/O ──────────────────────────────────────────────────────────────

    public Func<byte> ReadPortA = () => 0xFF;
    public Func<byte> ReadPortB = () => 0xFF;

    public byte PortALatch { get; private set; }
    public byte PortBLatch { get; private set; }

    private byte _ddra; // 1 = output, 0 = input
    private byte _ddrb;

    // ── Timers ────────────────────────────────────────────────────────────────

    private ushort _t1Latch;
    private int    _t1Counter;
    private bool   _t1Running;

    private byte   _t2LatchLo;
    private int    _t2Counter;
    private bool   _t2Running;

    // ── Interrupt control ─────────────────────────────────────────────────────

    private byte _acr; // auxiliary control register
    private byte _pcr; // peripheral control register
    private byte _ifr; // interrupt flags (bits 0-6); bit 7 computed on read
    private byte _ier; // interrupt enable (bits 0-6)

    // CA1/CB1 pin levels (for edge detection)
    private bool _ca1Level;
    private bool _cb1Level;

    private const byte IFR_CA1 = 0x02;
    private const byte IFR_CB1 = 0x10;
    private const byte IFR_T2  = 0x20;
    private const byte IFR_T1  = 0x40;
    private const byte IFR_ANY = 0x80;

    // ── IBus ──────────────────────────────────────────────────────────────────

    public byte Read(ushort address)
    {
        switch (address & 0xF)
        {
            case 0:  return ReadPortBCombined();
            case 1:  return ReadPortACombined();
            case 2:  return _ddrb;
            case 3:  return _ddra;
            case 4:  _ifr &= unchecked((byte)~IFR_T1); return (byte)(_t1Counter & 0xFF);
            case 5:  return (byte)((_t1Counter >> 8) & 0xFF);
            case 6:  return (byte)(_t1Latch & 0xFF);
            case 7:  return (byte)(_t1Latch >> 8);
            case 8:  _ifr &= unchecked((byte)~IFR_T2); return (byte)(_t2Counter & 0xFF);
            case 9:  return (byte)((_t2Counter >> 8) & 0xFF);
            case 11: return _acr;
            case 12: return _pcr;
            case 13: return (byte)(_ifr | ((_ifr & _ier) != 0 ? IFR_ANY : 0));
            case 14: return (byte)(_ier | IFR_ANY); // bit 7 always 1 on read
            default: return 0xFF;
        }
    }

    public void Write(ushort address, byte value)
    {
        switch (address & 0xF)
        {
            case 0:  PortBLatch = value; break;
            case 1:  PortALatch = value; break;
            case 2:  _ddrb = value; break;
            case 3:  _ddra = value; break;
            case 4:  _t1Latch = (ushort)((_t1Latch & 0xFF00) | value); break;
            case 5:
                _t1Latch   = (ushort)((_t1Latch & 0x00FF) | (value << 8));
                _t1Counter = _t1Latch;
                _t1Running = true;
                _ifr &= unchecked((byte)~IFR_T1);
                break;
            case 6:  _t1Latch = (ushort)((_t1Latch & 0xFF00) | value); break;
            case 7:
                _t1Latch = (ushort)((_t1Latch & 0x00FF) | (value << 8));
                _ifr &= unchecked((byte)~IFR_T1);
                break;
            case 8:  _t2LatchLo = value; break;
            case 9:
                _t2Counter = (_t2LatchLo | (value << 8));
                _t2Running = true;
                _ifr &= unchecked((byte)~IFR_T2);
                break;
            case 11: _acr = value; break;
            case 12: _pcr = value; break;
            case 13: _ifr &= (byte)~value; break;   // write 1s to clear flags
            case 14:
                if ((value & IFR_ANY) != 0)
                    _ier |= (byte)(value & 0x7F);    // bit 7 = 1: set enables
                else
                    _ier &= (byte)~value;             // bit 7 = 0: clear enables
                break;
        }
    }

    // ── Timing ────────────────────────────────────────────────────────────────

    /// <summary>Advance both timers by <paramref name="cycles"/> clock cycles.</summary>
    public void Tick(int cycles)
    {
        if (_t1Running)
        {
            _t1Counter -= cycles;
            if (_t1Counter <= 0)
            {
                _ifr |= IFR_T1;
                if ((_acr & 0x40) != 0)
                    _t1Counter += _t1Latch; // free-run: reload, preserving any overshoot
                else
                    _t1Running = false;
            }
        }

        if (_t2Running)
        {
            _t2Counter -= cycles;
            if (_t2Counter <= 0)
            {
                _ifr |= IFR_T2;
                _t2Running = false;
            }
        }
    }

    // ── IRQ line ──────────────────────────────────────────────────────────────

    /// <summary>True when an enabled interrupt is pending (IFR bit 7).</summary>
    public bool Irq => (_ifr & _ier) != 0;

    // ── CA1 / CB1 pin inputs ──────────────────────────────────────────────────

    /// <summary>
    /// Drive the CB1 pin. Sets IFR bit 4 on the active edge (PCR bit 4: 0=falling, 1=rising).
    /// </summary>
    public void SetCB1(bool level)
    {
        bool activeHigh = (_pcr & 0x10) != 0;
        bool edge = activeHigh ? (!_cb1Level && level) : (_cb1Level && !level);
        _cb1Level = level;
        if (edge) _ifr |= IFR_CB1;
    }

    /// <summary>
    /// Drive the CA1 pin. Sets IFR bit 1 on the active edge (PCR bit 0: 0=falling, 1=rising).
    /// </summary>
    public void SetCA1(bool level)
    {
        bool activeHigh = (_pcr & 0x01) != 0;
        bool edge = activeHigh ? (!_ca1Level && level) : (_ca1Level && !level);
        _ca1Level = level;
        if (edge) _ifr |= IFR_CA1;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private byte ReadPortBCombined()
    {
        byte input  = ReadPortB();
        byte output = PortBLatch;
        // Output pins return the latch; input pins return the delegate.
        return (byte)((output & _ddrb) | (input & ~_ddrb));
    }

    private byte ReadPortACombined()
    {
        byte input  = ReadPortA();
        byte output = PortALatch;
        return (byte)((output & _ddra) | (input & ~_ddra));
    }
}
