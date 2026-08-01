using Machines.Common;
using Xunit;

namespace Machines.Common.Tests;

public class Via6522Tests
{
    [Fact]
    public void Via_ReadWriteRegisters_Works()
    {
        var via = new Via6522();

        // DDRB / DDRA
        via.Write(0x02, 0xFF);
        via.Write(0x03, 0x0F);
        Assert.Equal(0xFF, via.Read(0x02));
        Assert.Equal(0x0F, via.Read(0x03));

        // PCR / ACR
        via.Write(0x0B, 0x40); // ACR free-running T1
        via.Write(0x0C, 0xCE); // PCR
        Assert.Equal(0x40, via.Read(0x0B));
        Assert.Equal(0xCE, via.Read(0x0C));
    }

    [Fact]
    public void Via_Timer1_OneShot_FiresInterrupt()
    {
        var via = new Via6522();
        via.Write(0x0E, 0xEC); // IER: enable T1 interrupt (bit 7 set)
        via.Write(0x04, 0x10); // T1L-L
        via.Write(0x05, 0x00); // T1H-H -> starts T1

        via.Tick(20);

        Assert.True(via.Irq);
        Assert.True((via.Read(0x0D) & 0x40) != 0); // IFR T1 bit set
    }

    [Fact]
    public void Via_Timer2_OneShot_FiresInterrupt()
    {
        var via = new Via6522();
        via.Write(0x0E, 0xA0); // IER: enable T2 interrupt
        via.Write(0x08, 0x0A); // T2L-L
        via.Write(0x09, 0x00); // T2H-H -> starts T2

        via.Tick(15);

        Assert.True(via.Irq);
        Assert.True((via.Read(0x0D) & 0x20) != 0); // IFR T2 bit set
    }

    [Fact]
    public void Via_ClearInterruptFlags_ByWritingIFR()
    {
        var via = new Via6522();
        via.Write(0x0E, 0xC0);
        via.Write(0x0D, 0x7F); // Write 1s to clear all flags

        Assert.False(via.Irq);
    }
}
