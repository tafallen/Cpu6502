using Machines.Oric;
using Xunit;

namespace Machines.Oric.Tests;

public class OricBootIntegrationTests
{
    [Fact]
    public void Headless_OricBootSequence_ExecutesResetAndInitializesMemory()
    {
        // Construct Atmos V1.1 dummy ROM with valid RESET vector pointing to $F000
        byte[] osRom = new byte[0x4000];
        // Reset vector at $FFFC/$FFFD -> offset 0x3FFC/0x3FFD
        osRom[0x3FFC] = 0x00;
        osRom[0x3FFD] = 0xF0; // $F000

        // Boot code at $F000 (offset 0x3000):
        // SEI, CLD, LDX #$FF, TXS, LDA #$55, STA $00, STA $BB80, BRK
        osRom[0x3000] = 0x78; // SEI
        osRom[0x3001] = 0xD8; // CLD
        osRom[0x3002] = 0xA2; // LDX #$FF
        osRom[0x3003] = 0xFF;
        osRom[0x3004] = 0x9A; // TXS
        osRom[0x3005] = 0xA9; // LDA #$55 ('U')
        osRom[0x3006] = 0x55;
        osRom[0x3007] = 0x85; // STA $00
        osRom[0x3008] = 0x00;
        osRom[0x3009] = 0x8D; // STA $BB80
        osRom[0x300A] = 0x80;
        osRom[0x300B] = 0xBB;
        osRom[0x300C] = 0x00; // BRK

        var machine = new OricMachine(osRom);
        machine.Reset();

        // Step 100 instructions
        for (int i = 0; i < 100; i++)
        {
            machine.Step();
        }

        // Assert zero-page memory and VRAM text screen ($BB80)
        Assert.Equal(0x55, machine.Ram.Read(0x0000));
        Assert.Equal(0x55, machine.Ram.Read(0xBB80));
    }
}
