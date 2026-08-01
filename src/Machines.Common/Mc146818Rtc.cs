using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cpu6502.Core;

namespace Machines.Common;

/// <summary>
/// Motorola MC146818 Real-Time Clock (RTC) and 50-byte non-volatile CMOS RAM.
/// Reused across BBC Master, IBM PC/AT, and arcade boards.
/// </summary>
public class Mc146818Rtc : IBus
{
    private readonly byte[] _cmosRam = new byte[64];
    private byte _selectedRegister;

    public Mc146818Rtc()
    {
        _cmosRam[0x0A] = 0x26;
        _cmosRam[0x0B] = 0x02;
    }

    public byte Read(ushort address)
    {
        if ((address & 1) == 0)
        {
            return _selectedRegister;
        }
        else
        {
            if (_selectedRegister < 64)
            {
                ref byte dataRef = ref MemoryMarshal.GetArrayDataReference(_cmosRam);
                return Unsafe.Add(ref dataRef, _selectedRegister);
            }
            return 0xFF;
        }
    }

    public void Write(ushort address, byte value)
    {
        if ((address & 1) == 0)
        {
            _selectedRegister = (byte)(value & 0x3F);
        }
        else
        {
            if (_selectedRegister < 64)
            {
                ref byte dataRef = ref MemoryMarshal.GetArrayDataReference(_cmosRam);
                Unsafe.Add(ref dataRef, _selectedRegister) = value;
            }
        }
    }
}
