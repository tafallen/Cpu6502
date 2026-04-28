using Cpu6502.Core;

namespace Machines.Atom;

/// <summary>
/// Intel 8255A Programmable Peripheral Interface — Mode 0 only.
/// Three 8-bit I/O ports (A, B, C) with direction configured via the control register.
/// Port C supports independent upper/lower nibble directions and per-bit set/reset.
/// </summary>
public sealed class Ppi8255 : IBus
{
    // Default control word: mode 0, all ports input (0x9B = 1001_1011)
    private byte _control = 0x9B;
    private byte _portALatch;
    private byte _portBLatch;
    private byte _portCLatch;

    // Callers inject these to supply the external pin state for input-configured ports.
    public Func<byte>  ReadPortA     { get; set; } = () => 0xFF;
    public Func<byte>  ReadPortB     { get; set; } = () => 0xFF;
    public Func<byte>  ReadPortC     { get; set; } = () => 0xFF;
    public Action<byte>? OnPortAWrite { get; set; }
    public Action<byte, byte>? OnPortBRead { get; set; } // (portALatch, portBResult)

    // Expose output latches so other chips can observe driven values without a bus read.
    public byte PortALatch => _portALatch;
    public byte PortBLatch => _portBLatch;
    public byte PortCLatch => _portCLatch;

    // Control word direction bits
    private bool PortAIsInput      => (_control & 0x10) != 0; // D4
    private bool PortBIsInput      => (_control & 0x02) != 0; // D1
    private bool PortCUpperIsInput => (_control & 0x08) != 0; // D3
    private bool PortCLowerIsInput => (_control & 0x01) != 0; // D0

    public byte Read(ushort address) => (address & 3) switch
    {
        0 => PortAIsInput ? ReadPortA() : _portALatch,
        1 => ReadPortBWithLog(),
        2 => ReadPortCMerged(),
        _ => _control
    };

    public void Write(ushort address, byte value)
    {
        switch (address & 3)
        {
            case 0: _portALatch = value; OnPortAWrite?.Invoke(value); break;
            case 1: _portBLatch = value; break;
            case 2: _portCLatch = value; break;
            case 3:
                if ((value & 0x80) != 0)
                    _control = value;
                else
                    ApplyPortCBitSetReset(value);
                break;
        }
    }

    private byte ReadPortBWithLog()
    {
        byte result = PortBIsInput ? ReadPortB() : _portBLatch;
        OnPortBRead?.Invoke(_portALatch, result);
        return result;
    }

    private byte ReadPortCMerged()
    {
        byte upper = PortCUpperIsInput ? (byte)(ReadPortC() & 0xF0) : (byte)(_portCLatch & 0xF0);
        byte lower = PortCLowerIsInput ? (byte)(ReadPortC() & 0x0F) : (byte)(_portCLatch & 0x0F);
        return (byte)(upper | lower);
    }

    private void ApplyPortCBitSetReset(byte value)
    {
        int bit = (value >> 1) & 7;
        if ((value & 1) != 0)
            _portCLatch |= (byte)(1 << bit);
        else
            _portCLatch &= (byte)~(1 << bit);
    }
}
