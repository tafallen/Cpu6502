using Machines.C64;
using Machines.Common;
using Xunit;

namespace Machines.C64.Tests;

public class Cia6526Tests
{
    [Fact]
    public void TimerA_Underflow_TriggersInterruptStatusAndIrq()
    {
        var cia = new Cia6526();

        // Enable Timer A Underflow IRQ mask (bit 0)
        cia.Write(0x0D, 0x81);

        // Load Timer A Latch = 0x0005
        cia.Write(0x04, 0x05);
        cia.Write(0x05, 0x00);

        // Start Timer A (CRA bit 0 = 1, bit 4 force load = 1)
        cia.Write(0x0E, 0x11);

        Assert.False(cia.Irq);

        // Tick 6 cycles -> should underflow
        cia.Tick(6);

        Assert.True(cia.Irq);

        // Read ICR ($0D) -> clears IRQ status
        byte icr = cia.Read(0x0D);
        Assert.Equal(0x81, icr);
        Assert.False(cia.Irq);
    }

    [Fact]
    public void KeyboardAdapter_MatrixScanning_SenseRowState()
    {
        var keyboard = new C64KeyboardAdapter();

        // Press key at Row 2, Column 3
        keyboard.KeyDown(2, 3);

        // Drive Column 3 LOW (bit 3 = 0 -> 0xF7)
        byte rowState = keyboard.ReadRowState(0xF7);

        // Bit 2 of rowState should be 0 (Row 2 sensed LOW) -> 0xFB
        Assert.Equal(0xFB, rowState);

        // Drive Column 0 LOW (bit 0 = 0 -> 0xFE)
        rowState = keyboard.ReadRowState(0xFE);
        Assert.Equal(0xFF, rowState); // Key at col 3 not sensed
    }
}
