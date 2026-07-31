using Machines.Pet;
using Xunit;

namespace Machines.Pet.Tests;

public class PetBootIntegrationTests
{
    [Fact]
    public void Headless_PetBootSequence_ExecutesResetAndInitializesMemory()
    {
        // Construct 28 KB PET dummy ROM ($9000–$FFFF)
        byte[] romData = new byte[0x7000];
        // Reset vector at $FFFC/$FFFD -> offset 0x6FFC/0x6FFD
        romData[0x6FFC] = 0x00;
        romData[0x6FFD] = 0xF0; // $F000

        // Boot code at $F000 (offset 0x6000):
        // SEI, CLD, LDX #$FF, TXS, LDA #$2A, STA $00, STA $8000, BRK
        romData[0x6000] = 0x78; // SEI
        romData[0x6001] = 0xD8; // CLD
        romData[0x6002] = 0xA2; // LDX #$FF
        romData[0x6003] = 0xFF;
        romData[0x6004] = 0x9A; // TXS
        romData[0x6005] = 0xA9; // LDA #$2A ('*')
        romData[0x6006] = 0x2A;
        romData[0x6007] = 0x85; // STA $00
        romData[0x6008] = 0x00;
        romData[0x6009] = 0x8D; // STA $8000
        romData[0x600A] = 0x00;
        romData[0x600B] = 0x80;
        romData[0x600C] = 0x00; // BRK

        var machine = new PetMachine(romData);
        machine.Reset();

        // Step 100 instructions
        for (int i = 0; i < 100; i++)
        {
            machine.Step();
        }

        // Assert zero page and Video RAM
        Assert.Equal(0x2A, machine.Ram.Read(0x0000));
        Assert.Equal(0x2A, machine.VideoRam.Read(0x0000));
    }
}
