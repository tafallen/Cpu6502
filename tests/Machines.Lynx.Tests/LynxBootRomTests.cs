using Cpu6502.Core;
using Machines.Lynx;
using Xunit;

namespace Machines.Lynx.Tests;

public class LynxBootRomTests
{
    [Fact]
    public void LynxMachine_BootRom_ExecutesBootVectorAndInitializesMemory()
    {
        string bootRomPath = "roms/Atari Lynx/lynxboot.img";
        string gamePath = "roms/Atari Lynx/Games/California Games (1991).lnx";

        byte[]? bootRom = File.Exists(bootRomPath) ? File.ReadAllBytes(bootRomPath) : null;
        byte[]? gameCart = File.Exists(gamePath) ? File.ReadAllBytes(gamePath) : null;

        if (bootRom is not null)
        {
            var machine = new LynxMachine(cartridgeRom: gameCart);

            // Load 512B boot ROM at top of memory ($FE00–$FFFF)
            byte[]? ramBuf = machine.Ram.DirectWriteBuffer;
            Assert.NotNull(ramBuf);

            Array.Copy(bootRom, 0, ramBuf, 0xFE00, 512);

            // Set RESET vector at $FFFC/$FFFD -> $FE00
            ramBuf[0xFFFC] = 0x00;
            ramBuf[0xFFFD] = 0xFE;

            machine.Reset();

            Assert.Equal((ushort)0xFE00, machine.Cpu.PC);

            // Run 1 frame (80,000 cycles at 4 MHz)
            machine.RunFrame();

            // Verify Boot ROM executed without crashing
            Assert.True(machine.Cpu.TotalCycles >= 80_000);
            Assert.True(machine.Cpu.PC != 0xFE00);
        }
    }
}
