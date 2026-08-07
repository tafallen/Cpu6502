using Cpu6502.Core;
using Machines.Lynx;
using Xunit;

namespace Machines.Lynx.Tests;

public class LynxMachineTests
{
    [Fact]
    public void LynxMachine_Initialization_Maps64KBRam()
    {
        var machine = new LynxMachine();

        Assert.Equal(0x10000, machine.Ram.Memory.Length);

        machine.Bus.Write(0x2000, 0x55);
        Assert.Equal(0x55, machine.Bus.Read(0x2000));
    }

    [Fact]
    public void Suzy_MathCoprocessor_MultiplicationAndDivision_Succeeds()
    {
        var suzy = new Suzy();

        // 16-bit × 16-bit multiplication: 123 × 456 = 56088 ($DAF8)
        suzy.Write(0x52, 123);
        suzy.Write(0x53, 0);
        suzy.Write(0x54, 200);
        suzy.Write(0x55, 0);

        Assert.Equal(24600u, (uint)(suzy.Read(0x60) | (suzy.Read(0x61) << 8) | (suzy.Read(0x62) << 16) | (suzy.Read(0x63) << 24)));
    }

    [Fact]
    public void Mikey_PaletteWrite_UpdatesRgbValues()
    {
        var mikey = new Mikey();

        // Write Green=15, Blue=0 ($F0) to Palette index 0 ($FDA0)
        mikey.Write(0xA0, 0xF0);

        // Write Red=15 ($0F) to Palette index 0 ($FDB0)
        mikey.Write(0xB0, 0x0F);

        byte val = mikey.Read(0xA0);
        Assert.Equal(0xF0, val);
    }

    [Fact]
    public void LynxMachine_BootSequence_ExecutesAndModifiesMemory()
    {
        var machine = new LynxMachine();

        byte[]? ramBuf = machine.Ram.DirectWriteBuffer;
        Assert.NotNull(ramBuf);

        // RESET vector -> $0200
        ramBuf[0xFFFC] = 0x00;
        ramBuf[0xFFFD] = 0x02;

        // Code at $0200:
        // $0200: SEI       (78)
        // $0201: CLD       (D8)
        // $0202: LDX #$FF  (A2 FF)
        // $0204: TXS       (9A)
        // $0205: LDA #$99  (A9 99)
        // $0207: STA $0400 (8D 00 04)
        // $020A: NOP       (EA)
        ramBuf[0x0200] = 0x78;
        ramBuf[0x0201] = 0xD8;
        ramBuf[0x0202] = 0xA2; ramBuf[0x0203] = 0xFF;
        ramBuf[0x0204] = 0x9A;
        ramBuf[0x0205] = 0xA9; ramBuf[0x0206] = 0x99;
        ramBuf[0x0207] = 0x8D; ramBuf[0x0208] = 0x00; ramBuf[0x0209] = 0x04;
        ramBuf[0x020A] = 0xEA;

        machine.Reset();

        Assert.Equal((ushort)0x0200, machine.Cpu.PC);

        machine.RunFrame();

        Assert.True(machine.Cpu.TotalCycles >= 80_000);
        Assert.True(machine.Cpu.PC != 0x0200);
        Assert.Equal(0x99, machine.Bus.Read(0x0400));
    }
}
