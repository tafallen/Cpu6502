using System.IO.Compression;
using System.Text;

namespace Machines.Atom;

public static class UefParser
{
    private static readonly byte[] GzipMagic = [0x1F, 0x8B];
    private const string UefMagic = "UEF File!";

    public static List<bool> Parse(Stream stream)
    {
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        var header = new byte[2];
        ms.Read(header, 0, 2);
        ms.Position = 0;

        Stream data = (header[0] == GzipMagic[0] && header[1] == GzipMagic[1])
            ? Decompress(ms)
            : ms;

        return ParseUncompressed(data);
    }

    private static MemoryStream Decompress(Stream compressed)
    {
        var result = new MemoryStream();
        using var gz = new GZipStream(compressed, CompressionMode.Decompress, leaveOpen: true);
        gz.CopyTo(result);
        result.Position = 0;
        return result;
    }

    private static List<bool> ParseUncompressed(Stream stream)
    {
        using var r = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        var magic = r.ReadBytes(9);
        if (Encoding.ASCII.GetString(magic) != UefMagic)
            throw new InvalidDataException("Not a UEF file.");
        r.ReadByte(); // null terminator
        r.ReadByte(); // minor version
        r.ReadByte(); // major version

        var bits = new List<bool>();

        while (stream.Position < stream.Length)
        {
            ushort chunkId  = r.ReadUInt16();
            int    chunkLen = r.ReadInt32();
            byte[] chunkData = r.ReadBytes(chunkLen);

            switch (chunkId)
            {
                case 0x0100: // implicit start/stop bit data
                    foreach (byte b in chunkData)
                    {
                        bits.Add(false); // start bit
                        for (int i = 0; i < 8; i++)
                            bits.Add(((b >> i) & 1) == 1);
                        bits.Add(true); // stop bit
                    }
                    break;

                case 0x0110: // carrier tone
                {
                    ushort cycles = BitConverter.ToUInt16(chunkData, 0);
                    int count = cycles / 8;
                    for (int i = 0; i < count; i++) bits.Add(true);
                    break;
                }

                case 0x0112: // integer gap (units: 1/20 second)
                {
                    ushort twentieths = BitConverter.ToUInt16(chunkData, 0);
                    int count = (int)(twentieths * 15); // N/20 sec × 300 baud
                    for (int i = 0; i < count; i++) bits.Add(true);
                    break;
                }

                case 0x0116: // floating-point gap (seconds)
                {
                    float seconds = BitConverter.ToSingle(chunkData, 0);
                    int count = (int)MathF.Round(seconds * 300f);
                    for (int i = 0; i < count; i++) bits.Add(true);
                    break;
                }
            }
        }

        return bits;
    }
}
