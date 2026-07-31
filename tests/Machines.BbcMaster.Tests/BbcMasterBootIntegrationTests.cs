using Machines.BbcMaster;
using Xunit;

namespace Machines.BbcMaster.Tests;

public class BbcMasterBootIntegrationTests
{
    [Fact]
    public void Headless_BbcMasterBootSequence_ExecutesResetAndInitializesMemory()
    {
        // Construct 16 KB MOS 3.20 dummy ROM ($C000–$FFFF)
        byte[] osRom = new byte[0x4000];
        // Reset vector at $FFFC/$FFFD and IRQ vector at $FFFE/$FFFF -> offset 0x3FFC–0x3FFF
        osRom[0x3FFC] = 0x00;
        osRom[0x3FFD] = 0xD0; // $D000
        osRom[0x3FFE] = 0x00;
        osRom[0x3FFF] = 0xD0; // $D000

        // Boot code at $D000 (offset 0x1000):
        // SEI, CLD, LDX #$FF, TXS, LDA #$42, STA $00, STA $7C00, NOP
        osRom[0x1000] = 0x78; // SEI
        osRom[0x1001] = 0xD8; // CLD
        osRom[0x1002] = 0xA2; // LDX #$FF
        osRom[0x1003] = 0xFF;
        osRom[0x1004] = 0x9A; // TXS
        osRom[0x1005] = 0xA9; // LDA #$42 ('B')
        osRom[0x1006] = 0x42;
        osRom[0x1007] = 0x85; // STA $00
        osRom[0x1008] = 0x00;
        osRom[0x1009] = 0x8D; // STA $7C00
        osRom[0x100A] = 0x00;
        osRom[0x100B] = 0x7C;
        osRom[0x100C] = 0xEA; // NOP

        var machine = new BbcMasterMachine(osRom);
        machine.Reset();

        // Step 10 instructions
        for (int i = 0; i < 10; i++)
        {
            machine.Step();
        }

        // Assert zero page and VRAM text screen ($7C00)
        Assert.Equal(0x42, machine.MainRam.Read(0x0000));
        Assert.Equal(0x42, machine.MainRam.Read(0x7C00));
    }
}
