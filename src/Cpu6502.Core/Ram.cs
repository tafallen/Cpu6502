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

    /// <summary>Read-only access to the backing buffer — for chips (e.g. VDG) that share the bus.</summary>
    public ReadOnlyMemory<byte> Memory => _data.AsMemory();

    /// <summary>Direct access to the backing buffer — for chips (e.g. VDG) that share the bus.</summary>
    [Obsolete("Use Memory property instead, which returns ReadOnlyMemory<byte>")]
    public byte[] RawBytes => _data;

    public void ValidateAddress(ushort address)
    {
        if (address >= _size)
            throw new InvalidOperationException(
                $"Ram access at 0x{address:X4} exceeds size 0x{_size:X4}");
    }

    public byte Read(ushort address)
    {
        // Bounds check in Release mode: return 0xFF (open bus) for out-of-bounds reads
        if (address >= _size)
            return 0xFF;
        return _data[address];
    }

    public void Write(ushort address, byte value)
    {
        // Bounds check in Release mode: silently ignore out-of-bounds writes
        if (address >= _size)
            return;
        _data[address] = value;
    }

    /// <summary>Copies <paramref name="data"/> into RAM starting at <paramref name="baseAddress"/>.</summary>
    public void Load(ushort baseAddress, byte[] data)
        => Array.Copy(data, 0, _data, baseAddress, data.Length);
}
