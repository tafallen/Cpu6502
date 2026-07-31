using Machines.C64;
using Xunit;

namespace Machines.C64.Tests;

public class C64BootIntegrationTests
{
    [Fact]
    public void HeadlessC64Boot_ExecutesInitializationRoutine()
    {
        byte[] kernal = new byte[0x2000];
        byte[] basic  = new byte[0x2000];
        byte[] chgen  = new byte[0x1000];

        // Setup Reset Vector at $FFFC/$FFFD -> $E000
        kernal[0x1FFC] = 0x00;
        kernal[0x1FFD] = 0xE0;

        // Boot Code at $E000:
        // SEI ($78), CLD ($D8), LDX #$FF ($A2 $FF), TXS ($9A),
        // LDA #$37 ($A9 $37), STA $01 ($85 $01),
        // LDA #$01 ($A9 $01), STA $D020 ($8D $20 $D0),
        // RTS ($60)
        byte[] bootCode = new byte[]
        {
            0x78, 0xD8, 0xA2, 0xFF, 0x9A,
            0xA9, 0x37, 0x85, 0x01,
            0xA9, 0x01, 0x8D, 0x20, 0xD0,
            0x60
        };
        Array.Copy(bootCode, 0, kernal, 0, bootCode.Length);

        var machine = new C64Machine(kernal, basic, chgen);
        machine.Reset();

        // Step 9 instructions
        for (int i = 0; i < 9; i++)
        {
            machine.Step();
        }

        // Verify MOS 6510 banking register ($01) = 0x37
        Assert.Equal(0x37, machine.Bus.Read(0x0001));

        // Verify VIC-II Border Color ($D020) = White (1)
        Assert.Equal(1, machine.Vic.BorderColor);
    }
}
