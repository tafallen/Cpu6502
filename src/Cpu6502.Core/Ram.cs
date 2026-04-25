namespace Cpu6502.Core;

/// <summary>Flat read/write RAM. Size is specified at construction.</summary>
public sealed class Ram : IBus
{
    private readonly byte[] _data;

    public Ram(int size) => _data = new byte[size];

    /// <summary>Direct access to the backing buffer — for chips (e.g. VDG) that share the bus.</summary>
    public byte[] RawBytes => _data;

    public byte Read(ushort address) => _data[address];

    public void Write(ushort address, byte value) => _data[address] = value;

    /// <summary>Copies <paramref name="data"/> into RAM starting at <paramref name="baseAddress"/>.</summary>
    public void Load(ushort baseAddress, byte[] data)
        => Array.Copy(data, 0, _data, baseAddress, data.Length);
}
