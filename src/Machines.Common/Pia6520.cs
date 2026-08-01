using Cpu6502.Core;

namespace Machines.Common;

/// <summary>
/// MOS 6520 Peripheral Interface Adapter (PIA) hardware controller.
/// Reused across Commodore PET ($E810–$E813) and Atari 800/800XL ($D300–$D303).
/// </summary>
public class Pia6520 : IBus
{
    public byte PortA { get; set; } = 0xFF;
    public byte PortB { get; set; } = 0xFF;
    public byte ControlA { get; set; }
    public byte ControlB { get; set; }

    public byte PortALatch { get => PortA; set => PortA = value; }
    public byte PortBLatch { get => PortB; set => PortB = value; }

    public Func<byte> ReadPortA = () => 0xFF;
    public Func<byte> ReadPortB = () => 0xFF;

    public byte Read(ushort address)
    {
        switch (address & 3)
        {
            case 0: return ReadPortA();
            case 1: return ControlA;
            case 2: return (byte)(ReadPortB() & PortB);
            case 3: return ControlB;
            default: return 0xFF;
        }
    }

    public void Write(ushort address, byte value)
    {
        switch (address & 3)
        {
            case 0: PortA = value; break;
            case 1: ControlA = value; break;
            case 2: PortB = value; break;
            case 3: ControlB = value; break;
        }
    }
}
