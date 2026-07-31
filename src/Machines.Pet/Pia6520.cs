using Cpu6502.Core;

namespace Machines.Pet;

/// <summary>
/// MOS 6520 Peripheral Interface Adapter (PIA) used in Commodore PET machines ($E810–$E813).
/// Used for keyboard row selection (Port A) and column readback (Port B).
/// </summary>
public sealed class Pia6520 : IBus
{
    public byte PortALatch { get; set; }
    public byte PortBLatch { get; set; }
    public byte ControlA { get; set; }
    public byte ControlB { get; set; }

    public Func<byte> ReadPortA = () => 0xFF;
    public Func<byte> ReadPortB = () => 0xFF;

    public byte Read(ushort address)
    {
        switch (address & 3)
        {
            case 0: return ReadPortA();
            case 1: return ControlA;
            case 2: return ReadPortB();
            case 3: return ControlB;
            default: return 0xFF;
        }
    }

    public void Write(ushort address, byte value)
    {
        switch (address & 3)
        {
            case 0: PortALatch = value; break;
            case 1: ControlA = value; break;
            case 2: PortBLatch = value; break;
            case 3: ControlB = value; break;
        }
    }
}
