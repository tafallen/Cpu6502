namespace Cpu6502.Core;

/// <summary>
/// Read-only memory. Constructed from a byte array. Writes are silently ignored,
/// matching real hardware — there is no bus error when the CPU writes to a ROM address.
/// </summary>
public sealed class Rom : IBus
{
    private readonly byte[] _data;

    public Rom(byte[] data) => _data = (byte[])data.Clone();

    public byte Read(ushort address) => _data[address];

    public void Write(ushort address, byte value) { /* ROM ignores writes */ }
}
