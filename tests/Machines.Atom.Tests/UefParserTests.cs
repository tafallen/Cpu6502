using System.Text;
using Machines.Atom;

namespace Machines.Atom.Tests;

public class UefParserTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static byte[] BuildUef(Action<BinaryWriter> writeChunks)
    {
        using var ms  = new MemoryStream();
        using var w   = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        w.Write(Encoding.ASCII.GetBytes("UEF File!"));
        w.Write((byte)0);   // null terminator
        w.Write((byte)10);  // minor version
        w.Write((byte)0);   // major version
        writeChunks(w);
        return ms.ToArray();
    }

    private static void WriteChunk(BinaryWriter w, ushort id, byte[] data)
    {
        w.Write(id);
        w.Write((int)data.Length);
        w.Write(data);
    }

    private static List<bool> Parse(byte[] uef) =>
        UefParser.Parse(new MemoryStream(uef));

    // ── header validation ─────────────────────────────────────────────────────

    [Fact]
    public void InvalidHeader_Throws()
    {
        var bad = Encoding.ASCII.GetBytes("NOT A UEF\x00\x00\x00");
        Assert.Throws<InvalidDataException>(() => UefParser.Parse(new MemoryStream(bad)));
    }

    [Fact]
    public void EmptyChunks_ReturnsEmptyBitStream()
    {
        var bits = Parse(BuildUef(_ => { }));
        Assert.Empty(bits);
    }

    // ── chunk 0x0100: implicit start/stop bit data ────────────────────────────

    [Fact]
    public void DataChunk_SingleByte_Produces10Bits()
    {
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0100, [0x00])));
        Assert.Equal(10, bits.Count);
    }

    [Fact]
    public void DataChunk_StartBitIsLow()
    {
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0100, [0xFF])));
        Assert.False(bits[0]); // start bit always 0
    }

    [Fact]
    public void DataChunk_StopBitIsHigh()
    {
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0100, [0x00])));
        Assert.True(bits[9]); // stop bit always 1
    }

    [Fact]
    public void DataChunk_0x00_AllDataBitsLow()
    {
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0100, [0x00])));
        // bits 1-8 = 8 data bits, all 0 for 0x00
        for (int i = 1; i <= 8; i++) Assert.False(bits[i]);
    }

    [Fact]
    public void DataChunk_0xFF_AllDataBitsHigh()
    {
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0100, [0xFF])));
        for (int i = 1; i <= 8; i++) Assert.True(bits[i]);
    }

    [Fact]
    public void DataChunk_0x55_AlternatingBits()
    {
        // 0x55 = 0101_0101; LSB first → 1,0,1,0,1,0,1,0
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0100, [0x55])));
        // bit 0 = start (false), bits 1-8 = data LSB first, bit 9 = stop (true)
        bool[] expected = [false, true, false, true, false, true, false, true, false, true];
        Assert.Equal(expected, bits);
    }

    [Fact]
    public void DataChunk_0xAA_AlternatingBits()
    {
        // 0xAA = 1010_1010; LSB first → 0,1,0,1,0,1,0,1
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0100, [0xAA])));
        bool[] expected = [false, false, true, false, true, false, true, false, true, true];
        Assert.Equal(expected, bits);
    }

    [Fact]
    public void DataChunk_TwoBytes_Produces20Bits()
    {
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0100, [0x00, 0xFF])));
        Assert.Equal(20, bits.Count);
    }

    [Fact]
    public void DataChunk_LsbFirst_ByteOrder()
    {
        // 0x01 = 0000_0001; LSB first → 1,0,0,0,0,0,0,0
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0100, [0x01])));
        Assert.False(bits[0]); // start
        Assert.True(bits[1]);  // bit 0 = 1
        for (int i = 2; i <= 8; i++) Assert.False(bits[i]); // bits 1-7 = 0
        Assert.True(bits[9]);  // stop
    }

    // ── chunk 0x0110: carrier tone ────────────────────────────────────────────

    [Fact]
    public void CarrierChunk_ProducesHighBits()
    {
        // 800 cycles → 100 carrier bits (800/8)
        byte[] data = BitConverter.GetBytes((ushort)800);
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0110, data)));
        Assert.Equal(100, bits.Count);
        Assert.All(bits, b => Assert.True(b));
    }

    [Fact]
    public void CarrierChunk_ZeroCycles_ProducesNoBits()
    {
        byte[] data = BitConverter.GetBytes((ushort)0);
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0110, data)));
        Assert.Empty(bits);
    }

    // ── chunk 0x0112: integer gap ─────────────────────────────────────────────

    [Fact]
    public void IntegerGapChunk_ProducesHighBits()
    {
        // 20 twentieths = 1 second = 300 bits of silence
        byte[] data = BitConverter.GetBytes((ushort)20);
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0112, data)));
        Assert.Equal(300, bits.Count);
        Assert.All(bits, b => Assert.True(b));
    }

    // ── chunk 0x0116: floating point gap ──────────────────────────────────────

    [Fact]
    public void FloatGapChunk_OneSecond_Produces300Bits()
    {
        byte[] data = BitConverter.GetBytes(1.0f);
        var bits = Parse(BuildUef(w => WriteChunk(w, 0x0116, data)));
        Assert.Equal(300, bits.Count);
        Assert.All(bits, b => Assert.True(b));
    }

    // ── unknown chunks are silently skipped ───────────────────────────────────

    [Fact]
    public void UnknownChunk_IsSkipped()
    {
        var bits = Parse(BuildUef(w =>
        {
            WriteChunk(w, 0xFFFF, [0xDE, 0xAD]); // unknown
            WriteChunk(w, 0x0100, [0x00]);        // known
        }));
        Assert.Equal(10, bits.Count); // only the data chunk
    }

    // ── multiple chunks concatenate ───────────────────────────────────────────

    [Fact]
    public void MultipleDataChunks_BitStreamConcatenates()
    {
        var bits = Parse(BuildUef(w =>
        {
            WriteChunk(w, 0x0100, [0x00]);
            WriteChunk(w, 0x0100, [0xFF]);
        }));
        Assert.Equal(20, bits.Count);
    }

    // ── gzip-compressed UEF ───────────────────────────────────────────────────

    [Fact]
    public void GzipCompressed_ParsedCorrectly()
    {
        var raw = BuildUef(w => WriteChunk(w, 0x0100, [0x55]));
        using var compressedMs = new MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(
                   compressedMs, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
            gz.Write(raw);

        compressedMs.Seek(0, SeekOrigin.Begin);
        var bits = UefParser.Parse(compressedMs);
        Assert.Equal(10, bits.Count);
        bool[] expected = [false, true, false, true, false, true, false, true, false, true];
        Assert.Equal(expected, bits);
    }
}
