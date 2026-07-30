using Machines.BbcMicro;
using Xunit;

namespace Machines.BbcMicro.Tests;

public class BbcMicroBootIntegrationTests
{
    [Fact]
    public void Headless_BbcBootSequence_ExecutesResetAndInitializesMemory()
    {
        // Construct OS 1.20 dummy ROM with valid RESET vector pointing to $D000
        byte[] osRom = new byte[0x4000];
        // Reset vector at $FFFC/$FFFD -> offset $3FFC/$3FFD in 16KB OS ROM
        osRom[0x3FFC] = 0x00;
        osRom[0x3FFD] = 0xD0; // $D000

        // At $D000: SEI, CLD, LDX #$FF, TXS, LDA #$01, STA $00, BRK
        osRom[0x1000] = 0x78; // SEI
        osRom[0x1001] = 0xD8; // CLD
        osRom[0x1002] = 0xA2; // LDX #$FF
        osRom[0x1003] = 0xFF;
        osRom[0x1004] = 0x9A; // TXS
        osRom[0x1005] = 0xA9; // LDA #$01
        osRom[0x1006] = 0x01;
        osRom[0x1007] = 0x85; // STA $00
        osRom[0x1008] = 0x00;
        osRom[0x1009] = 0x00; // BRK

        var machine = new BbcMicroMachine(osRom);
        machine.Reset();

        // Run 100 steps
        for (int i = 0; i < 100; i++)
        {
            machine.Step();
        }

        // Verify zero-page location $00 was written with 1
        Assert.Equal(1, machine.Ram.Read(0x0000));
    }
}
