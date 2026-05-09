using System;

namespace Adapters.Gdb;

/// <summary>
/// Represents a GDB Remote Serial Protocol (RSP) packet.
/// Format: $&lt;data&gt;#&lt;checksum&gt;
/// </summary>
public sealed class RspPacket
{
    /// <summary>Raw packet data (without $ prefix and #checksum suffix).</summary>
    public string Data { get; }

    /// <summary>Packet checksum (2 hex digits).</summary>
    public string Checksum { get; }

    public RspPacket(string data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Checksum = CalculateChecksum(data);
    }

    /// <summary>Encode packet as $data#checksum for transmission.</summary>
    public string Encode() => $"${Data}#{Checksum}";

    /// <summary>Calculate RSP checksum: sum of all bytes mod 256.</summary>
    private static string CalculateChecksum(string data)
    {
        byte sum = 0;
        foreach (char c in data)
            sum += (byte)c;
        return sum.ToString("X2");
    }

    /// <summary>Parse RSP packet from wire format ($data#checksum).</summary>
    public static RspPacket Parse(string wireData)
    {
        if (!wireData.StartsWith("$"))
            throw new ArgumentException("Packet must start with $", nameof(wireData));
        if (!wireData.Contains("#"))
            throw new ArgumentException("Packet must contain #", nameof(wireData));

        int hashIndex = wireData.LastIndexOf('#');
        string data = wireData[1..hashIndex];
        string checksum = wireData[(hashIndex + 1)..];

        // Verify checksum
        string expectedChecksum = new RspPacket(data).Checksum;
        if (checksum != expectedChecksum)
            throw new ArgumentException($"Checksum mismatch: got {checksum}, expected {expectedChecksum}", nameof(wireData));

        return new RspPacket(data);
    }

    /// <summary>Extract command character (first byte of data).</summary>
    public char Command => Data.Length > 0 ? Data[0] : '\0';

    /// <summary>Extract command arguments (data after first byte).</summary>
    public string Arguments => Data.Length > 1 ? Data[1..] : "";

    public override string ToString() => Encode();
}
