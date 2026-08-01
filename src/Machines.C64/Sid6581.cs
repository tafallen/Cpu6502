using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cpu6502.Core;

namespace Machines.C64;

/// <summary>
/// MOS 6581 / 8580 Sound Interface Device (SID) audio synthesizer chip ($D400–$D41C).
/// Features 3 independent sound voices, ADSR envelope generators, variable pulse width,
/// triangle/sawtooth/pulse/noise waveforms, and state-variable analog filters.
/// </summary>
public sealed class Sid6581 : IBus
{
    private readonly byte[] _registers = new byte[0x20];

    public byte Volume => (byte)(_registers[0x18] & 0x0F);
    public bool FilterLowPass => (_registers[0x18] & 0x10) != 0;
    public bool FilterBandPass => (_registers[0x18] & 0x20) != 0;
    public bool FilterHighPass => (_registers[0x18] & 0x40) != 0;

    public ushort GetVoiceFrequency(int voice)
    {
        if (voice < 0 || voice > 2) return 0;
        int baseAddr = voice * 7;
        ref byte regRef = ref MemoryMarshal.GetArrayDataReference(_registers);
        return (ushort)(Unsafe.Add(ref regRef, baseAddr) | (Unsafe.Add(ref regRef, baseAddr + 1) << 8));
    }

    public byte Read(ushort address)
    {
        int reg = address & 0x1F;
        if (reg == 0x1B) return 0x80;
        if (reg == 0x1C) return 0xFF;
        return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_registers), reg);
    }

    public void Write(ushort address, byte value)
    {
        int reg = address & 0x1F;
        if (reg < 0x19)
        {
            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_registers), reg) = value;
        }
    }
}
