using Machines.Common;
using Xunit;

namespace Machines.Common.Tests;

public class Cia6526Tests
{
    [Fact]
    public void Cia_ReadWriteRegisters_Works()
    {
        var cia = new Cia6526();

        cia.Write(0x02, 0xFF); // DDRA
        cia.Write(0x03, 0x00); // DDRB
        Assert.Equal(0xFF, cia.Read(0x02));
        Assert.Equal(0x00, cia.Read(0x03));

        cia.Write(0x00, 0xAA); // PRA
        Assert.Equal(0xAA, cia.Read(0x00));
    }

    [Fact]
    public void Cia_TimerA_Underflow_FiresInterrupt()
    {
        var cia = new Cia6526();
        cia.Write(0x0D, 0x81); // ICR: enable Timer A interrupt
        cia.Write(0x04, 0x05); // Timer A Latch Low
        cia.Write(0x05, 0x00); // Timer A Latch High
        cia.Write(0x0E, 0x01); // CRA: Start Timer A

        cia.Tick(10);

        Assert.True(cia.Irq);
        byte icr = cia.Read(0x0D);
        Assert.True((icr & 0x01) != 0); // Timer A underflow bit set
        Assert.False(cia.Irq); // Reading ICR clears interrupt
    }

    [Fact]
    public void Cia_TimerB_Underflow_FiresInterrupt()
    {
        var cia = new Cia6526();
        cia.Write(0x0D, 0x82); // ICR: enable Timer B interrupt
        cia.Write(0x06, 0x05); // Timer B Latch Low
        cia.Write(0x07, 0x00); // Timer B Latch High
        cia.Write(0x0F, 0x01); // CRB: Start Timer B

        cia.Tick(10);

        Assert.True(cia.Irq);
        byte icr = cia.Read(0x0D);
        Assert.True((icr & 0x02) != 0);
    }
}
