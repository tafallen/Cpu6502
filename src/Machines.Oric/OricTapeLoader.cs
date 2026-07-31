using Cpu6502.Core;

namespace Machines.Oric;

public sealed record OricTapeHeader(
    string Name,
    ushort LoadAddress,
    ushort EndAddress,
    byte AutoRun,
    byte ProgramType
);

/// <summary>
/// Cassette tape image loader for Oric-1 / Oric Atmos (.tap format).
/// Format specification:
/// - 3 to 13 Sync bytes 0x16 followed by 0x24 (header marker)
/// - Program type byte (0x00 = BASIC, 0x80 = Machine Code)
/// - Auto-run flag byte (0x00 = No, 0xC7 = Auto-run)
/// - End address (16-bit hi/lo)
/// - Load address (16-bit hi/lo)
/// - Reserved byte
/// - Null-terminated filename ASCII string
/// - Payload bytes ($LoadAddress to $EndAddress)
/// </summary>
public static class OricTapeLoader
{
    public static OricTapeHeader LoadTape(byte[] tapData, Ram ram)
    {
        if (tapData.Length < 16)
            throw new ArgumentException("TAP data too short", nameof(tapData));

        int idx = 0;
        // Skip leading 0x16 sync bytes
        while (idx < tapData.Length && tapData[idx] == 0x16)
            idx++;

        if (idx >= tapData.Length || tapData[idx] != 0x24)
            throw new InvalidDataException("Invalid Oric TAP header marker");

        idx++; // skip 0x24
        byte programType = tapData[idx++];
        byte autoRun = tapData[idx++];

        ushort endAddr = (ushort)((tapData[idx] << 8) | tapData[idx + 1]);
        idx += 2;
        ushort loadAddr = (ushort)((tapData[idx] << 8) | tapData[idx + 1]);
        idx += 2;

        idx++; // skip reserved byte

        // Read null-terminated string
        int strStart = idx;
        while (idx < tapData.Length && tapData[idx] != 0x00)
            idx++;
        string name = System.Text.Encoding.ASCII.GetString(tapData, strStart, idx - strStart);
        idx++; // skip null byte

        // Write payload to RAM
        int payloadLen = Math.Max(0, endAddr - loadAddr + 1);
        int bytesToCopy = Math.Min(payloadLen, tapData.Length - idx);

        if (bytesToCopy > 0)
        {
            ram.Load(loadAddr, tapData.AsSpan(idx, bytesToCopy).ToArray());
        }

        return new OricTapeHeader(name, loadAddr, endAddr, autoRun, programType);
    }
}
