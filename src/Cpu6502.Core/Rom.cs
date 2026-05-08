namespace Cpu6502.Core;

/// <summary>
/// Read-only memory. Constructed from a byte array. Writes are silently ignored,
/// matching real hardware — there is no bus error when the CPU writes to a ROM address.
/// Out-of-bounds reads return 0xFF (open bus); out-of-bounds writes are silently ignored.
/// </summary>
public sealed class Rom : IBusValidator
{
    private readonly byte[] _data;
    private readonly int _size;

    public Rom(byte[] data)
    {
        _data = (byte[])data.Clone();
        _size = data.Length;
    }

    public void ValidateAddress(ushort address)
    {
        if (address >= _size)
            throw new InvalidOperationException(
                $"Rom access at 0x{address:X4} exceeds size 0x{_size:X4}");
    }

    public byte Read(ushort address)
    {
        // Bounds check in Release mode: return 0xFF (open bus) for out-of-bounds reads
        if (address >= _size)
            return 0xFF;
        return _data[address];
    }

    public void Write(ushort address, byte value) { /* ROM ignores writes */ }
}
