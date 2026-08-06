using Cpu6502.Core;
using System.Runtime.CompilerServices;

namespace Machines.Common;

/// <summary>
/// Atari POKEY (POtentiometer and KEYboard) custom integrated circuit ($D200–$D20F).
/// Features 4 independent audio frequency channels, high-pass filters, noise shift registers,
/// 64-key keyboard matrix scanner, and serial I/O clocking.
/// </summary>
public sealed class Pokey : IBus
{
    private readonly byte[] _registers = new byte[0x10];
    private ushort _randomState = 0xACE1; // LFSR seed

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte NextRandom()
    {
        _randomState = (ushort)(_randomState * 25173 + 13849);
        return (byte)(_randomState >> 8);
    }


    public byte Kbcode { get; set; } = 0xFF;
    public byte Skstat { get; set; } = 0xFF;
    public byte IrqEnable { get; private set; }
    public byte IrqStatus { get; private set; } = 0xFF;

    public bool Irq => (IrqStatus & 0xE0) != 0;

    public Func<byte>? ReadKeyboard { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ushort address)
    {
        switch (address & 0x0F)
        {
            case 0x08: return NextRandom();
            case 0x09: return ReadKeyboard?.Invoke() ?? Kbcode; // KBCODE
            case 0x0A: return NextRandom();
            case 0x0E: return IrqStatus;
            case 0x0F: return Skstat;
            default: return _registers[address & 0x0F];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort address, byte value)
    {
        byte reg = (byte)(address & 0x0F);
        _registers[reg] = value;

        if (reg == 0x0E) // IRQEN
        {
            IrqEnable = value;
            IrqStatus = (byte)(~value & 0xFF);
        }
        else if (reg == 0x0F) // SKCTL
        {
            if ((value & 0x03) == 0)
            {
                // Reset keyboard scanner
                Kbcode = 0xFF;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TriggerKeypress(byte keyCode)
    {
        Kbcode = keyCode;
        if ((IrqEnable & 0x01) != 0) // Keyboard IRQ enable
        {
            IrqStatus &= 0xFE; // Clear bit 0 (key pressed IRQ active)
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Tick(int cycles = 1)
    {
        // POKEY audio timer dividers tick
    }
}
