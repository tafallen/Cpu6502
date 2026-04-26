using Machines.Vic20;

namespace Machines.Vic20.Tests;

public class TapParserTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static Stream MakeTap(byte version, params byte[] dataBytes)
    {
        var ms = new MemoryStream();
        ms.Write("C64-TAPE-RAW"u8); // 12 bytes
        ms.WriteByte(version);       // version
        ms.WriteByte(0); ms.WriteByte(0); ms.WriteByte(0); // reserved
        uint len = (uint)dataBytes.Length;
        ms.Write([
            (byte)(len & 0xFF),
            (byte)((len >> 8) & 0xFF),
            (byte)((len >> 16) & 0xFF),
            (byte)((len >> 24) & 0xFF)
        ]);
        ms.Write(dataBytes);
        ms.Position = 0;
        return ms;
    }

    // ── invalid header ────────────────────────────────────────────────────────

    [Fact]
    public void BadMagic_Throws()
    {
        var ms = new MemoryStream("NOT-A-TAP-FILE"u8.ToArray());
        Assert.Throws<InvalidDataException>(() => TapParser.Parse(ms));
    }

    // ── version 0: byte × 8 ───────────────────────────────────────────────────

    [Fact]
    public void Version0_SingleByte_ReturnsCycleTimesEight()
    {
        var pulses = TapParser.Parse(MakeTap(0, 0x20)); // 32 × 8 = 256
        Assert.Single(pulses);
        Assert.Equal(256, pulses[0]);
    }

    [Fact]
    public void Version0_MultipleBytes_EachMultipliedByEight()
    {
        var pulses = TapParser.Parse(MakeTap(0, 0x10, 0x20, 0x40));
        Assert.Equal(3, pulses.Length);
        Assert.Equal(0x10 * 8, pulses[0]);
        Assert.Equal(0x20 * 8, pulses[1]);
        Assert.Equal(0x40 * 8, pulses[2]);
    }

    [Fact]
    public void Version0_ZeroByte_IsDecodedAsZeroTimes8()
    {
        // Version 0 has no extended encoding — 0x00 means 0 × 8 = 0 cycles
        var pulses = TapParser.Parse(MakeTap(0, 0x00));
        Assert.Single(pulses);
        Assert.Equal(0, pulses[0]);
    }

    // ── version 1: zero byte → 24-bit extended ────────────────────────────────

    [Fact]
    public void Version1_NonZeroByte_StillMultipliedByEight()
    {
        var pulses = TapParser.Parse(MakeTap(1, 0x20));
        Assert.Single(pulses);
        Assert.Equal(0x20 * 8, pulses[0]);
    }

    [Fact]
    public void Version1_ZeroByte_UsesNext3BytesAsLittleEndianCount()
    {
        // Extended value 0x001234 = 4660
        var pulses = TapParser.Parse(MakeTap(1, 0x00, 0x34, 0x12, 0x00));
        Assert.Single(pulses);
        Assert.Equal(0x001234, pulses[0]);
    }

    [Fact]
    public void Version1_MixedNormalAndExtended_DecodesCorrectly()
    {
        var pulses = TapParser.Parse(MakeTap(1,
            0x10,               // 0x10 × 8 = 128
            0x00, 0xFF, 0x00, 0x00, // extended 0x0000FF = 255
            0x20                // 0x20 × 8 = 256
        ));
        Assert.Equal(3, pulses.Length);
        Assert.Equal(128, pulses[0]);
        Assert.Equal(255, pulses[1]);
        Assert.Equal(256, pulses[2]);
    }

    // ── empty data ────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyData_ReturnsEmptyArray()
    {
        var pulses = TapParser.Parse(MakeTap(0));
        Assert.Empty(pulses);
    }
}
