using Cpu6502.Core;
using Machines.Plus4;
using Xunit;

namespace Machines.Plus4.Tests;

public class Plus4MachineTests
{
    private static (byte[] kernal, byte[] basic) CreateMockRoms()
    {
        var kernal = new byte[0x4000];
        var basic = new byte[0x4000];

        // RESET vector at $FFFC/$FFFD -> $E000
        kernal[0x3FFC] = 0x00;
        kernal[0x3FFD] = 0xE0;

        return (kernal, basic);
    }

    [Fact]
    public void Plus4Machine_Initialization_C16_Maps16KBRam()
    {
        var (kernal, basic) = CreateMockRoms();
        var machine = new Plus4Machine(kernal, basic, model: Plus4Model.C16);

        Assert.Equal(Plus4Model.C16, machine.Model);
        Assert.Equal(0x4000, machine.Ram.Memory.Length);

        machine.Bus.Write(0x3FFF, 0x42);
        Assert.Equal(0x42, machine.Bus.Read(0x3FFF));

        // Address > $3FFF returns open bus on C16
        machine.Bus.Write(0x4000, 0x99);
        Assert.Equal(0xFF, machine.Bus.Read(0x4000));
    }

    [Fact]
    public void Plus4Machine_Initialization_Plus4_Maps64KBRam()
    {
        var (kernal, basic) = CreateMockRoms();
        var machine = new Plus4Machine(kernal, basic, model: Plus4Model.Plus4);

        Assert.Equal(Plus4Model.Plus4, machine.Model);
        Assert.Equal(0xFE00, machine.Ram.Memory.Length);

        machine.Bus.Write(0x4000, 0x55);
        Assert.Equal(0x55, machine.Bus.Read(0x4000));
    }

    [Fact]
    public void Ted7360_TimersAndInterrupts_DecrementAndRaiseIrq()
    {
        var ted = new Ted7360();
        ted.Write(0x00, 0x05); // Timer 1 low
        ted.Write(0x01, 0x00); // Timer 1 high + reload

        ted.Write(0x0A, 0x02); // Enable Timer 1 IRQ (bit 1)

        ted.Tick(10); // Tick 10 cycles -> should trigger reload and IRQ

        Assert.True(ted.Irq);
        Assert.Equal(0x02, ted.IrqStatus & 0x02);
    }

    [Fact]
    public void Plus4Machine_LoadPrg_CopiesProgramIntoRam()
    {
        var (kernal, basic) = CreateMockRoms();
        var machine = new Plus4Machine(kernal, basic, model: Plus4Model.Plus4);

        byte[] prgData = new byte[] { 0x00, 0x10, 0xEA, 0xEA, 0x60 }; // Load at $1000: NOP, NOP, RTS
        ushort loadAddr = Plus4Machine.LoadPrg(prgData, machine);

        Assert.Equal((ushort)0x1000, loadAddr);
        Assert.Equal(0xEA, machine.Bus.Read(0x1000));
        Assert.Equal(0xEA, machine.Bus.Read(0x1001));
        Assert.Equal(0x60, machine.Bus.Read(0x1002));
    }
}
