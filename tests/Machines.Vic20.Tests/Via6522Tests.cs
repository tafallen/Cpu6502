using Machines.Vic20;

namespace Machines.Vic20.Tests;

public class Via6522Tests
{
    // Register offsets
    private const ushort ORB  = 0;   // Port B data
    private const ushort ORA  = 1;   // Port A data
    private const ushort DDRB = 2;   // Port B direction
    private const ushort DDRA = 3;   // Port A direction
    private const ushort T1LL = 4;   // Timer 1 latch/counter low
    private const ushort T1LH = 5;   // Timer 1 latch/counter high (write starts timer)
    private const ushort T1AL = 6;   // Timer 1 latch low (no counter reload)
    private const ushort T1AH = 7;   // Timer 1 latch high (no counter reload)
    private const ushort T2L  = 8;   // Timer 2 latch/counter low
    private const ushort T2H  = 9;   // Timer 2 counter high (write starts timer)
    private const ushort ACR  = 11;  // Auxiliary control register
    private const ushort IFR  = 13;  // Interrupt flag register
    private const ushort IER  = 14;  // Interrupt enable register

    // IFR/IER bit positions
    private const byte IFR_T2 = 0x20; // bit 5
    private const byte IFR_T1 = 0x40; // bit 6
    private const byte IFR_ANY = 0x80; // bit 7 — set when any enabled interrupt is active

    // ACR bits
    private const byte ACR_T1_FREERUN = 0x40; // bit 6: T1 free-run mode

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Via6522 Make() => new();

    // Write a 16-bit value into a pair of VIA registers (lo then hi).
    private static void WriteWord(Via6522 via, ushort loReg, ushort hiReg, ushort value)
    {
        via.Write(loReg, (byte)(value & 0xFF));
        via.Write(hiReg, (byte)(value >> 8));
    }

    // ── DDR ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Ddrb_DefaultsToAllInput()
    {
        Assert.Equal(0x00, Make().Read(DDRB));
    }

    [Fact]
    public void Ddra_DefaultsToAllInput()
    {
        Assert.Equal(0x00, Make().Read(DDRA));
    }

    [Fact]
    public void WriteDdrb_RoundTrips()
    {
        var via = Make();
        via.Write(DDRB, 0xF0);
        Assert.Equal(0xF0, via.Read(DDRB));
    }

    [Fact]
    public void WriteDdra_RoundTrips()
    {
        var via = Make();
        via.Write(DDRA, 0x0F);
        Assert.Equal(0x0F, via.Read(DDRA));
    }

    // ── Port B I/O ────────────────────────────────────────────────────────────

    [Fact]
    public void PortB_WriteToOutputPins_StoredInLatch()
    {
        var via = Make();
        via.Write(DDRB, 0xFF); // all output
        via.Write(ORB, 0xAB);
        Assert.Equal(0xAB, via.PortBLatch);
    }

    [Fact]
    public void PortB_ReadOutputPins_ReturnsLatch()
    {
        var via = Make();
        via.Write(DDRB, 0xFF);
        via.Write(ORB, 0xAB);
        Assert.Equal(0xAB, via.Read(ORB));
    }

    [Fact]
    public void PortB_ReadInputPins_ReturnsDelegate()
    {
        var via = Make();
        via.Write(DDRB, 0x00); // all input
        via.ReadPortB = () => 0x55;
        Assert.Equal(0x55, via.Read(ORB));
    }

    [Fact]
    public void PortB_MixedDirection_OutputPinsFromLatch_InputPinsFromDelegate()
    {
        // Upper nibble = output, lower nibble = input
        var via = Make();
        via.Write(DDRB, 0xF0);
        via.Write(ORB, 0xA0);       // output latch = upper nibble $A
        via.ReadPortB = () => 0x05; // delegate drives lower nibble
        byte result = via.Read(ORB);
        Assert.Equal(0xA5, result);
    }

    // ── Port A I/O ────────────────────────────────────────────────────────────

    [Fact]
    public void PortA_WriteToOutputPins_StoredInLatch()
    {
        var via = Make();
        via.Write(DDRA, 0xFF);
        via.Write(ORA, 0xCD);
        Assert.Equal(0xCD, via.PortALatch);
    }

    [Fact]
    public void PortA_ReadInputPins_ReturnsDelegate()
    {
        var via = Make();
        via.Write(DDRA, 0x00);
        via.ReadPortA = () => 0x77;
        Assert.Equal(0x77, via.Read(ORA));
    }

    [Fact]
    public void PortA_MixedDirection_CombinesLatchAndDelegate()
    {
        var via = Make();
        via.Write(DDRA, 0xF0);
        via.Write(ORA, 0xB0);
        via.ReadPortA = () => 0x03;
        Assert.Equal(0xB3, via.Read(ORA));
    }

    // ── Timer 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public void T1_WriteHighByte_LoadsCounterAndStartsTimer()
    {
        var via = Make();
        WriteWord(via, T1LL, T1LH, 10);
        // After 10 ticks the timer should fire; after 9 it should not
        via.Tick(9);
        Assert.Equal(0x00, via.Read(IFR) & IFR_T1);
    }

    [Fact]
    public void T1_FiresInterruptAfterCount()
    {
        var via = Make();
        WriteWord(via, T1LL, T1LH, 10);
        via.Tick(10);
        Assert.NotEqual(0, via.Read(IFR) & IFR_T1);
    }

    [Fact]
    public void T1_ReadingT1CounterLow_ClearsT1InterruptFlag()
    {
        var via = Make();
        WriteWord(via, T1LL, T1LH, 5);
        via.Tick(5);
        Assert.NotEqual(0, via.Read(IFR) & IFR_T1); // flag set
        via.Read(T1LL);                               // reading counter lo clears flag
        Assert.Equal(0, via.Read(IFR) & IFR_T1);
    }

    [Fact]
    public void T1_OneShotMode_DoesNotReloadAfterTimeout()
    {
        var via = Make();
        WriteWord(via, T1LL, T1LH, 5);
        via.Tick(5);  // fires once
        via.Write(IFR, IFR_T1); // clear flag
        via.Tick(100); // run much longer
        Assert.Equal(0, via.Read(IFR) & IFR_T1); // no second fire
    }

    [Fact]
    public void T1_FreeRunMode_ReloadsAndFiresAgain()
    {
        var via = Make();
        via.Write(ACR, ACR_T1_FREERUN);
        WriteWord(via, T1LL, T1LH, 10);
        via.Tick(10);  // first fire
        Assert.NotEqual(0, via.Read(IFR) & IFR_T1);
        via.Write(IFR, IFR_T1); // clear flag
        via.Tick(10);  // second fire
        Assert.NotEqual(0, via.Read(IFR) & IFR_T1);
    }

    [Fact]
    public void T1_WritingLatchRegs_DoesNotRestartCounter()
    {
        // Writing T1AL/T1AH updates latches only; does not reload or restart
        var via = Make();
        WriteWord(via, T1LL, T1LH, 20); // start timer at 20
        via.Tick(5);
        via.Write(T1AL, 0x05); // update latch — must not restart the countdown
        via.Write(T1AH, 0x00);
        via.Tick(15); // 5 + 15 = 20 ticks → should fire at original count
        Assert.NotEqual(0, via.Read(IFR) & IFR_T1);
    }

    // ── Timer 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public void T2_WriteHighByte_StartsTimer()
    {
        var via = Make();
        WriteWord(via, T2L, T2H, 10);
        via.Tick(9);
        Assert.Equal(0, via.Read(IFR) & IFR_T2);
    }

    [Fact]
    public void T2_FiresInterruptAfterCount()
    {
        var via = Make();
        WriteWord(via, T2L, T2H, 10);
        via.Tick(10);
        Assert.NotEqual(0, via.Read(IFR) & IFR_T2);
    }

    [Fact]
    public void T2_OneShotMode_DoesNotReload()
    {
        var via = Make();
        WriteWord(via, T2L, T2H, 5);
        via.Tick(5);
        via.Write(IFR, IFR_T2);
        via.Tick(100);
        Assert.Equal(0, via.Read(IFR) & IFR_T2);
    }

    [Fact]
    public void T2_ReadingCounterLow_ClearsT2Flag()
    {
        var via = Make();
        WriteWord(via, T2L, T2H, 5);
        via.Tick(5);
        Assert.NotEqual(0, via.Read(IFR) & IFR_T2);
        via.Read(T2L);
        Assert.Equal(0, via.Read(IFR) & IFR_T2);
    }

    // ── IER ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Ier_DefaultAllDisabled()
    {
        // bit 7 is always 1 on read; bits 0-6 reflect enables
        Assert.Equal(0x80, Make().Read(IER));
    }

    [Fact]
    public void Ier_WriteBit7Set_EnablesSpecifiedBits()
    {
        var via = Make();
        via.Write(IER, 0x80 | IFR_T1); // enable T1
        Assert.NotEqual(0, via.Read(IER) & IFR_T1);
    }

    [Fact]
    public void Ier_WriteBit7Clear_DisablesSpecifiedBits()
    {
        var via = Make();
        via.Write(IER, 0x80 | IFR_T1); // enable
        via.Write(IER, IFR_T1);         // disable (bit 7 = 0)
        Assert.Equal(0, via.Read(IER) & IFR_T1);
    }

    // ── IFR bit 7 (any interrupt) ─────────────────────────────────────────────

    [Fact]
    public void Ifr_Bit7_NotSetWhenInterruptFiredButNotEnabled()
    {
        var via = Make();
        // T1 fires but IER has T1 disabled
        WriteWord(via, T1LL, T1LH, 5);
        via.Tick(5);
        Assert.NotEqual(0, via.Read(IFR) & IFR_T1);  // flag set
        Assert.Equal(0, via.Read(IFR) & IFR_ANY);     // but IRQ line not asserted
    }

    [Fact]
    public void Ifr_Bit7_SetWhenEnabledInterruptFires()
    {
        var via = Make();
        via.Write(IER, 0x80 | IFR_T1);
        WriteWord(via, T1LL, T1LH, 5);
        via.Tick(5);
        Assert.NotEqual(0, via.Read(IFR) & IFR_ANY);
    }

    [Fact]
    public void Ifr_WritingFlagBit_ClearsIt()
    {
        var via = Make();
        via.Write(IER, 0x80 | IFR_T1);
        WriteWord(via, T1LL, T1LH, 5);
        via.Tick(5);
        via.Write(IFR, IFR_T1);
        Assert.Equal(0, via.Read(IFR) & IFR_T1);
        Assert.Equal(0, via.Read(IFR) & IFR_ANY);
    }

    // ── IRQ output ────────────────────────────────────────────────────────────

    [Fact]
    public void Irq_NotAssertedByDefault()
    {
        Assert.False(Make().Irq);
    }

    [Fact]
    public void Irq_AssertedWhenEnabledInterruptFires()
    {
        var via = Make();
        via.Write(IER, 0x80 | IFR_T1);
        WriteWord(via, T1LL, T1LH, 5);
        via.Tick(5);
        Assert.True(via.Irq);
    }

    [Fact]
    public void Irq_DeassertedAfterFlagCleared()
    {
        var via = Make();
        via.Write(IER, 0x80 | IFR_T1);
        WriteWord(via, T1LL, T1LH, 5);
        via.Tick(5);
        via.Write(IFR, IFR_T1);
        Assert.False(via.Irq);
    }
}
