using Cpu6502.Core;

namespace Machines.Common;

/// <summary>
/// MOS 6526 / 8520 Complex Interface Adapter (CIA) hardware implementation.
/// High-performance lazy timer decrement implementation.
/// </summary>
public sealed class Cia6526 : IBus
{
    public byte Pra { get; set; } = 0xFF;
    public byte Prb { get; set; } = 0xFF;
    public byte Ddra { get; set; }
    public byte Ddrb { get; set; }

    public ushort TimerACounter { get; private set; }
    public ushort TimerALatch { get; set; } = 0xFFFF;
    public bool TimerAStarted { get; private set; }

    public ushort TimerBCounter { get; private set; }
    public ushort TimerBLatch { get; set; } = 0xFFFF;
    public bool TimerBStarted { get; private set; }

    public byte ControlA { get; set; }
    public byte ControlB { get; set; }

    public byte InterruptEnable { get; private set; }
    public byte InterruptStatus { get; private set; }

    public bool Irq => (InterruptStatus & 0x80) != 0;

    public Func<byte>? ReadPortA { get; set; }
    public Func<byte>? ReadPortB { get; set; }

    public byte Read(ushort address)
    {
        switch (address & 0x0F)
        {
            case 0x00:
                byte paInput = ReadPortA?.Invoke() ?? 0xFF;
                return (byte)((Pra & Ddra) | (paInput & ~Ddra));

            case 0x01:
                byte pbInput = ReadPortB?.Invoke() ?? 0xFF;
                return (byte)((Prb & Ddrb) | (pbInput & ~Ddrb));

            case 0x02: return Ddra;
            case 0x03: return Ddrb;

            case 0x04: return (byte)(TimerACounter & 0xFF);
            case 0x05: return (byte)(TimerACounter >> 8);

            case 0x06: return (byte)(TimerBCounter & 0xFF);
            case 0x07: return (byte)(TimerBCounter >> 8);

            case 0x0D:
                byte icr = InterruptStatus;
                InterruptStatus = 0;
                return icr;

            case 0x0E: return ControlA;
            case 0x0F: return ControlB;

            default:
                return 0xFF;
        }
    }

    public void Write(ushort address, byte value)
    {
        switch (address & 0x0F)
        {
            case 0x00: Pra = value; break;
            case 0x01: Prb = value; break;
            case 0x02: Ddra = value; break;
            case 0x03: Ddrb = value; break;

            case 0x04: TimerALatch = (ushort)((TimerALatch & 0xFF00) | value); break;
            case 0x05:
                TimerALatch = (ushort)((TimerALatch & 0x00FF) | (value << 8));
                if ((ControlA & 0x10) != 0 || !TimerAStarted)
                {
                    TimerACounter = TimerALatch;
                }
                break;

            case 0x06: TimerBLatch = (ushort)((TimerBLatch & 0xFF00) | value); break;
            case 0x07:
                TimerBLatch = (ushort)((TimerBLatch & 0x00FF) | (value << 8));
                if ((ControlB & 0x10) != 0 || !TimerBStarted)
                {
                    TimerBCounter = TimerBLatch;
                }
                break;

            case 0x0D:
                if ((value & 0x80) != 0)
                {
                    InterruptEnable |= (byte)(value & 0x1F);
                }
                else
                {
                    InterruptEnable &= (byte)~(value & 0x1F);
                }
                break;

            case 0x0E:
                ControlA = value;
                TimerAStarted = (value & 0x01) != 0;
                if ((value & 0x10) != 0) TimerACounter = TimerALatch;
                break;

            case 0x0F:
                ControlB = value;
                TimerBStarted = (value & 0x01) != 0;
                if ((value & 0x10) != 0) TimerBCounter = TimerBLatch;
                break;
        }
    }

    public void Tick(int cycles = 1)
    {
        if (cycles <= 0 || (!TimerAStarted && !TimerBStarted)) return;

        if (TimerAStarted)
        {
            if (cycles >= TimerACounter)
            {
                TimerACounter = TimerALatch;
                TriggerInterrupt(0x01);
            }
            else
            {
                TimerACounter = (ushort)(TimerACounter - cycles);
            }
        }

        if (TimerBStarted)
        {
            if (cycles >= TimerBCounter)
            {
                TimerBCounter = TimerBLatch;
                TriggerInterrupt(0x02);
            }
            else
            {
                TimerBCounter = (ushort)(TimerBCounter - cycles);
            }
        }
    }

    private void TriggerInterrupt(byte mask)
    {
        InterruptStatus |= mask;
        if ((InterruptEnable & mask) != 0)
        {
            InterruptStatus |= 0x80;
        }
    }
}
