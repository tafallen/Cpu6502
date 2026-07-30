namespace Cpu6502.Core;

/// <summary>
/// Routes CPU bus traffic to hardware components by address range.
/// Multiple ranges may be registered; the last registration wins on overlap.
/// Unmapped reads return 0xFF (open bus); unmapped writes are silent.
/// 
/// Internally, routing is precomputed per address when Map(...) is called.
/// Direct array buffers for RAM/ROM are cached per route to bypass interface dispatch.
/// </summary>
public sealed class AddressDecoder : IBus
{
    private readonly record struct Route(IBus? Device, ushort From, byte[]? DirectReadBuffer, byte[]? DirectWriteBuffer);

    private readonly Route[] _routes = new Route[0x10000];

    /// <summary>Register <paramref name="device"/> for addresses [<paramref name="from"/>..<paramref name="to"/>] inclusive.</summary>
    public void Map(ushort from, ushort to, IBus device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (from > to)
            throw new ArgumentException("'from' must be less than or equal to 'to'.", nameof(from));

        byte[]? readBuf = (device as IDirectMemoryDevice)?.DirectReadBuffer;
        byte[]? writeBuf = (device as IDirectMemoryDevice)?.DirectWriteBuffer;

        // Precompute per-address routing so reads/writes are O(1).
        // Last mapping wins naturally as later maps overwrite earlier entries.
        for (int address = from; address <= to; address++)
            _routes[address] = new Route(device, from, readBuf, writeBuf);
    }

    public byte Read(ushort address)
    {
        ref readonly var route = ref _routes[address];
        if (route.DirectReadBuffer is byte[] buffer)
        {
            ushort offset = (ushort)(address - route.From);
            if ((uint)offset < (uint)buffer.Length)
                return buffer[offset];
        }
        if (route.Device is not null)
        {
            ushort offset = (ushort)(address - route.From);
#if DEBUG
            if (route.Device is IBusValidator validator)
                validator.ValidateAddress(offset);
#endif
            return route.Device.Read(offset);
        }
        return 0xFF;
    }

    public void Write(ushort address, byte value)
    {
        ref readonly var route = ref _routes[address];
        if (route.DirectWriteBuffer is byte[] buffer)
        {
            ushort offset = (ushort)(address - route.From);
            if ((uint)offset < (uint)buffer.Length)
            {
                buffer[offset] = value;
                return;
            }
        }
        if (route.Device is not null)
        {
            ushort offset = (ushort)(address - route.From);
#if DEBUG
            if (route.Device is IBusValidator validator)
                validator.ValidateAddress(offset);
#endif
            route.Device.Write(offset, value);
        }
    }

    public bool TryGetSpan(ushort address, int length, out ReadOnlySpan<byte> span)
    {
        ref readonly var route = ref _routes[address];
        if (route.DirectReadBuffer is byte[] buffer)
        {
            ushort offset = (ushort)(address - route.From);
            if (offset + length <= buffer.Length)
            {
                span = buffer.AsSpan(offset, length);
                return true;
            }
        }
        if (route.Device is not null)
        {
            ushort offset = (ushort)(address - route.From);
            return route.Device.TryGetSpan(offset, length, out span);
        }
        span = default;
        return false;
    }

    /// <summary>
    /// Validate all mapped address ranges by checking that every mapped device
    /// can handle its minimum address offset (0x0000). Called after Map() operations
    /// to catch misconfiguration early in DEBUG builds.
    /// </summary>
    public void ValidateMapping()
    {
#if DEBUG
        var checkedDevices = new HashSet<IBus>();
        for (int address = 0; address < 0x10000; address++)
        {
            ref readonly var route = ref _routes[address];
            if (route.Device is not null && checkedDevices.Add(route.Device))
            {
                // Check device at its base offset (first address it's mapped to)
                if (route.Device is IBusValidator validator)
                {
                    validator.ValidateAddress(0);
                }
            }
        }
#endif
    }
}
