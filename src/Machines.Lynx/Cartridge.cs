using System;

namespace Machines.Lynx;

/// <summary>
/// Emulates the Atari Lynx Cartridge hardware.
/// </summary>
public sealed class Cartridge
{
    private readonly byte[] _rom;
    private readonly int _blockSize;
    
    private int _shiftRegister;
    private int _counter;
    private bool _lastStrobe;

    public Cartridge(byte[] cartBytes)
    {
        if (cartBytes.Length >= 64 &&
            cartBytes[0] == 'L' && cartBytes[1] == 'Y' &&
            cartBytes[2] == 'N' && cartBytes[3] == 'X')
        {
            _blockSize = cartBytes[4] | (cartBytes[5] << 8);
            if (_blockSize == 0)
            {
                _blockSize = 1024;
            }

            int payloadLength = cartBytes.Length - 64;
            _rom = new byte[payloadLength];
            Array.Copy(cartBytes, 64, _rom, 0, payloadLength);
        }
        else
        {
            // Raw dump (e.g. unencrypted homebrew without LNX header)
            _blockSize = 1024;
            _rom = cartBytes;
        }

        // Initialize shift register to block 0 so first byte stream reads block 0
        _shiftRegister = 0;
    }

    /// <summary>
    /// Receives strobe and data signals from the Lynx I/O registers.
    /// </summary>
    public void SetStrobe(bool strobe, bool data)
    {
        if (strobe)
        {
            _counter = 0;
        }

        // Rising edge of strobe clocks the shift register
        if (strobe && !_lastStrobe)
        {
            _shiftRegister = ((_shiftRegister << 1) | (data ? 1 : 0)) & 0xFF;
        }
        _lastStrobe = strobe;
    }

    /// <summary>
    /// Reads a byte from the currently selected block and increments the byte counter.
    /// </summary>
    public byte ReadBank0()
    {
        int wrappedCounter = _counter % _blockSize;
        int address = (_shiftRegister * _blockSize) + wrappedCounter;
        byte value = 0xFF; // Default open bus
        
        if (address < _rom.Length)
        {
            value = _rom[address];
        }
        
        _counter++;
        _counter &= 0x07FF; // 11-bit ripple counter
        return value;
    }
}
