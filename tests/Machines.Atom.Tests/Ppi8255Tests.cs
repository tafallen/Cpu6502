namespace Machines.Atom.Tests;

public class Ppi8255Tests
{
    private readonly Ppi8255 _ppi = new();

    // --- default state (all ports input after reset) ---

    [Fact]
    public void DefaultState_ReadPortA_ReturnsDelegateValue()
    {
        _ppi.ReadPortA = () => 0x42;
        Assert.Equal(0x42, _ppi.Read(0));
    }

    [Fact]
    public void DefaultState_ReadPortB_ReturnsDelegateValue()
    {
        _ppi.ReadPortB = () => 0x55;
        Assert.Equal(0x55, _ppi.Read(1));
    }

    [Fact]
    public void DefaultState_ReadPortC_ReturnsDelegateValue()
    {
        _ppi.ReadPortC = () => 0x33;
        Assert.Equal(0x33, _ppi.Read(2));
    }

    // --- output port configuration ---

    [Fact]
    public void WriteControl_AllOutput_PortAReadBacksLatch()
    {
        _ppi.Write(3, 0x80); // mode set, all ports output
        _ppi.Write(0, 0xAB);
        Assert.Equal(0xAB, _ppi.Read(0));
    }

    [Fact]
    public void WriteControl_AllOutput_PortBReadBacksLatch()
    {
        _ppi.Write(3, 0x80);
        _ppi.Write(1, 0xCD);
        Assert.Equal(0xCD, _ppi.Read(1));
    }

    [Fact]
    public void WriteControl_AllOutput_PortCReadBacksLatch()
    {
        _ppi.Write(3, 0x80);
        _ppi.Write(2, 0xEF);
        Assert.Equal(0xEF, _ppi.Read(2));
    }

    // --- Acorn Atom configuration: PA=out, PB=in, PC=out (control 0x82) ---

    [Fact]
    public void AtomConfig_PortA_IsOutput()
    {
        _ppi.Write(3, 0x82);
        _ppi.Write(0, 0xFE);
        Assert.Equal(0xFE, _ppi.Read(0));
    }

    [Fact]
    public void AtomConfig_PortB_IsInput()
    {
        _ppi.Write(3, 0x82);
        _ppi.ReadPortB = () => 0x3F;
        Assert.Equal(0x3F, _ppi.Read(1));
    }

    [Fact]
    public void AtomConfig_PortC_IsOutput()
    {
        _ppi.Write(3, 0x82);
        _ppi.Write(2, 0x05);
        Assert.Equal(0x05, _ppi.Read(2));
    }

    // --- Port C bit set/reset (control byte with bit 7 = 0) ---

    [Fact]
    public void BitSetReset_SetsNamedBit()
    {
        _ppi.Write(3, 0x80);
        _ppi.Write(2, 0x00);
        _ppi.Write(3, 0x03); // bit 1, set
        Assert.Equal(0x02, _ppi.Read(2));
    }

    [Fact]
    public void BitSetReset_ClearsNamedBit()
    {
        _ppi.Write(3, 0x80);
        _ppi.Write(2, 0xFF);
        _ppi.Write(3, 0x02); // bit 1, clear
        Assert.Equal(0xFD, _ppi.Read(2));
    }

    [Fact]
    public void BitSetReset_AllBitsRoundTrip()
    {
        _ppi.Write(3, 0x80);
        _ppi.Write(2, 0x00);
        for (int bit = 0; bit < 8; bit++)
        {
            _ppi.Write(3, (byte)((bit << 1) | 0x01)); // set bit
            Assert.Equal((byte)(1 << bit), _ppi.Read(2));
            _ppi.Write(3, (byte)(bit << 1)); // clear bit
            Assert.Equal(0x00, _ppi.Read(2));
        }
    }

    // --- mixed Port C directions ---

    [Fact]
    public void PortC_UpperInput_LowerOutput_MergesCorrectly()
    {
        // 0x88 = 1000_1000: PA=out, PC-upper=in, PB=out, PC-lower=out
        _ppi.Write(3, 0x88);
        _ppi.ReadPortC = () => 0xF0;
        _ppi.Write(2, 0x05);
        Assert.Equal(0xF5, _ppi.Read(2)); // upper from delegate, lower from latch
    }

    [Fact]
    public void PortC_UpperOutput_LowerInput_MergesCorrectly()
    {
        // 0x81 = 1000_0001: PA=out, PC-upper=out, PB=out, PC-lower=in
        _ppi.Write(3, 0x81);
        _ppi.ReadPortC = () => 0x0A;
        _ppi.Write(2, 0xB0);
        Assert.Equal(0xBA, _ppi.Read(2)); // upper from latch, lower from delegate
    }

    // --- latch accessors for chip interconnect ---

    [Fact]
    public void PortALatch_ExposedForChipWiring()
    {
        _ppi.Write(3, 0x80);
        _ppi.Write(0, 0xFE);
        Assert.Equal(0xFE, _ppi.PortALatch);
    }

    [Fact]
    public void PortCLatch_ExposedForChipWiring()
    {
        _ppi.Write(3, 0x80);
        _ppi.Write(2, 0x07);
        Assert.Equal(0x07, _ppi.PortCLatch);
    }

    // --- write to input port does not affect delegate read ---

    [Fact]
    public void WriteToInputPort_DoesNotAffectDelegateRead()
    {
        _ppi.ReadPortA = () => 0x99;
        _ppi.Write(0, 0x11);
        Assert.Equal(0x99, _ppi.Read(0));
    }

    // --- address masking: upper bits of address are ignored ---

    [Fact]
    public void AddressMasking_HighBitsIgnored()
    {
        _ppi.Write(3, 0x80);
        _ppi.Write(0, 0xAA);
        Assert.Equal(0xAA, _ppi.Read(0x100)); // 0x100 & 3 == 0
    }
}
