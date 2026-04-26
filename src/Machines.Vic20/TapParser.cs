using System.Text;

namespace Machines.Vic20;

/// <summary>
/// Parses Commodore TAP cassette image files into an array of pulse widths
/// measured in VIC-20 clock cycles (1,108,405 Hz).
/// </summary>
public static class TapParser
{
    private const string Magic = "C64-TAPE-RAW";

    public static int[] Parse(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        var magic = r.ReadBytes(12);
        if (Encoding.ASCII.GetString(magic) != Magic)
            throw new InvalidDataException("Not a C64 TAP file.");

        byte version  = r.ReadByte();
        r.ReadByte(); r.ReadByte(); r.ReadByte(); // reserved
        uint dataLen  = r.ReadUInt32();

        var pulses = new List<int>();
        uint read = 0;

        while (read < dataLen)
        {
            byte b = r.ReadByte();
            read++;

            if (b != 0 || version == 0)
            {
                pulses.Add(b * 8);
            }
            else
            {
                // Version 1 extended: next 3 bytes are LE 24-bit cycle count
                byte lo  = r.ReadByte();
                byte mid = r.ReadByte();
                byte hi  = r.ReadByte();
                read += 3;
                pulses.Add(lo | (mid << 8) | (hi << 16));
            }
        }

        return [.. pulses];
    }
}
