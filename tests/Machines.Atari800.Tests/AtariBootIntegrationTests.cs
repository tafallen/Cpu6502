using Machines.Atari800;
using Xunit;

namespace Machines.Atari800.Tests;

public class AtariBootIntegrationTests
{
    [Fact]
    public void Atari800Machine_ResetAndStep_ExecutesWithoutCrashing()
    {
        var machine = new Atari800Machine();
        machine.Bus.Ram.Write(0xFFFC, 0x00);
        machine.Bus.Ram.Write(0xFFFD, 0x06);
        machine.Bus.Ram.Write(0x0600, 0xEA); // NOP

        machine.Reset();
        Assert.Equal(0x0600, machine.Cpu.PC);

        machine.Step();
        Assert.Equal(0x0601, machine.Cpu.PC);
    }
}
