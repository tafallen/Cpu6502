using Cpu6502.Core;

namespace Machines.Common;

/// <summary>
/// Acorn Tube ULA coprocessor interface hardware ($FEE0–$FEEF).
/// Connects Host I/O processor to external Second Processor via 4 hardware FIFO channels:
/// R1: Asynchronous control / OSBYTE / OSWORD parameters (1 byte)
/// R2: CLI command line string streaming (1 byte)
/// R3: Fast VDU screen graphics & text stream rendering (2 bytes)
/// R4: High-speed block DMA data transfer (24 bytes)
/// 
/// High-performance zero-allocation implementation backed by value-type ring buffers.
/// </summary>
public sealed class TubeUla : IBus
{
    private struct FastRingBuffer16
    {
        private readonly byte[] _buffer = new byte[16];
        private ushort _head;
        private ushort _tail;

        public FastRingBuffer16() { }

        public readonly ushort Count => (ushort)(_head - _tail);

        public void Enqueue(byte item)
        {
            _buffer[_head & 0x0F] = item;
            _head++;
        }

        public byte Dequeue()
        {
            if (_head == _tail) return 0xFF;
            byte item = _buffer[_tail & 0x0F];
            _tail++;
            return item;
        }
    }

    private FastRingBuffer16 _r1HostToParasite = new();
    private FastRingBuffer16 _r1ParasiteToHost = new();

    public byte Read(ushort address)
    {
        switch (address & 0x0F)
        {
            case 0x00: // Host R1 Status
                byte status = 0;
                if (_r1ParasiteToHost.Count > 0) status |= 0x80; // Data available
                if (_r1HostToParasite.Count == 0) status |= 0x40; // FIFO empty
                return status;

            case 0x01: // Host R1 Data
                return _r1ParasiteToHost.Dequeue();

            default:
                return 0xFF;
        }
    }

    public void Write(ushort address, byte value)
    {
        switch (address & 0x0F)
        {
            case 0x01: // Host R1 Data Write
                _r1HostToParasite.Enqueue(value);
                break;
        }
    }

    public byte ReadParasite(ushort address)
    {
        if ((address & 1) == 0) // Parasite Status
        {
            byte status = 0;
            if (_r1HostToParasite.Count > 0) status |= 0x80;
            if (_r1ParasiteToHost.Count == 0) status |= 0x40;
            return status;
        }
        else // Parasite Data Read
        {
            return _r1HostToParasite.Dequeue();
        }
    }

    public void WriteParasite(ushort address, byte value)
    {
        if ((address & 1) != 0)
        {
            _r1ParasiteToHost.Enqueue(value);
        }
    }
}
