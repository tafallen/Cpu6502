namespace Cpu6502.Core;

/// <summary>
/// Routes CPU bus traffic to hardware components by address range.
/// Multiple ranges may be registered; the last registration wins on overlap.
/// Unmapped reads return 0xFF (open bus); unmapped writes are silent.
/// 
/// Internally, routing is precomputed per address when Map(...) is called.
/// This shifts work to map-time so read/write dispatch stays O(1).
/// </summary>
public sealed class AddressDecoder : IBus
{
    private readonly record struct Mapping(ushort From, ushort To, IBus Device);
    private readonly record struct Route(IBus? Device, ushort From);

    private readonly List<Mapping> _mappings = new();
    private readonly Route[] _routes = new Route[0x10000];

    /// <summary>Register <paramref name="device"/> for addresses [<paramref name="from"/>..<paramref name="to"/>] inclusive.</summary>
    public void Map(ushort from, ushort to, IBus device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (from > to)
            throw new ArgumentException("'from' must be less than or equal to 'to'.", nameof(from));

        _mappings.Add(new Mapping(from, to, device));

        // Precompute per-address routing so reads/writes are O(1).
        // Last mapping wins naturally as later maps overwrite earlier entries.
        for (int address = from; address <= to; address++)
            _routes[address] = new Route(device, from);
    }

    public byte Read(ushort address)
    {
        var (device, from) = _routes[address];
#if DEBUG
        ValidateRoute(address, device, from);
#endif
        if (device is not null)
        {
            ushort offset = (ushort)(address - from);
#if DEBUG
            if (device is IBusValidator validator)
                validator.ValidateAddress(offset);
#endif
            return device.Read(offset);
        }
        return 0xFF;
    }

    public void Write(ushort address, byte value)
    {
        var (device, from) = _routes[address];
#if DEBUG
        ValidateRoute(address, device, from);
#endif
        if (device is not null)
        {
            ushort offset = (ushort)(address - from);
#if DEBUG
            if (device is IBusValidator validator)
                validator.ValidateAddress(offset);
#endif
            device.Write(offset, value);
        }
    }

#if DEBUG
    private void ValidateRoute(ushort address, IBus? device, ushort from)
    {
        var expected = ResolveWithBase(address);
        if (!ReferenceEquals(device, expected.Device) || from != expected.From)
            throw new InvalidOperationException($"AddressDecoder route mismatch at 0x{address:X4}.");
    }

    // Slow path used only for debug route verification.
    private (IBus? Device, ushort From) ResolveWithBase(ushort address)
    {
        for (int i = _mappings.Count - 1; i >= 0; i--)
        {
            var m = _mappings[i];
            if (address >= m.From && address <= m.To)
                return (m.Device, m.From);
        }
        return (null, 0);
    }
#endif
}
