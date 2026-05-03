namespace Cpu6502.Core;

/// <summary>Flat read/write RAM. Size is specified at construction.</summary>
public sealed class Ram : IBusValidator
{
    private readonly byte[] _data;
    private readonly int _size;

    public Ram(int size)
    {
        _size = size;
        _data = new byte[size];
    }

    /// <summary>Direct access to the backing buffer — for chips (e.g. VDG) that share the bus.</summary>
    public byte[] RawBytes => _data;

    public void ValidateAddress(ushort address)
    {
        if (address >= _size)
            throw new InvalidOperationException(
                $"Ram access at 0x{address:X4} exceeds size 0x{_size:X4}");
    }

    public byte Read(ushort address) => _data[address];

    public void Write(ushort address, byte value) => _data[address] = value;

    /// <summary>Copies <paramref name="data"/> into RAM starting at <paramref name="baseAddress"/>.</summary>
    public void Load(ushort baseAddress, byte[] data)
        => Array.Copy(data, 0, _data, baseAddress, data.Length);
}
