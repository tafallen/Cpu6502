using Cpu6502.Core;
using Xunit;

namespace Cpu6502.Tests;

/// <summary>
/// Klaus Dörmann's 6502 functional test suite.
/// Download 6502_functional_test.bin from:
///   https://github.com/Klaus2m5/6502_65C02_functional_tests
/// and place it in tests/Cpu6502.Tests/TestData/6502_functional_test.bin
///
/// A correct 6502 implementation runs the test to completion at PC=$3469.
/// Any bug traps the CPU in an infinite loop at a different address.
/// </summary>
public class IntegrationTests
{
    private const string BinPath = "TestData/6502_functional_test.bin";
    private const ushort EntryPoint   = 0x0400;
    private const ushort SuccessPC    = 0x3469;
    private const int    MaxSteps     = 100_000_000;  // plenty for the full suite

    [Fact]
    public void KlausDormann_FunctionalTest_RunsToCompletion()
    {
        if (!File.Exists(BinPath))
        {
            // Binary not present — test is vacuously green.
            // To run: place 6502_functional_test.bin in tests/Cpu6502.Tests/TestData/
            return;
        }

        byte[] program = File.ReadAllBytes(BinPath);

        var ram = new Ram(0x10000);
        ram.Load(0x0000, program);

        // Write the entry point directly into the reset vector
        ram.Write(0xFFFC, (byte)(EntryPoint & 0xFF));
        ram.Write(0xFFFD, (byte)(EntryPoint >> 8));

        var cpu = new Cpu(ram);
        cpu.Reset();

        ushort prevPc = 0;
        for (int i = 0; i < MaxSteps; i++)
        {
            cpu.Step();

            if (cpu.PC == SuccessPC)
                return;   // test passed

            // Trap detection: if PC hasn't changed after a step, we're in a loop
            if (cpu.PC == prevPc)
                Assert.Fail($"CPU trapped at PC=${cpu.PC:X4} after {i + 1} steps");

            prevPc = cpu.PC;
        }

        Assert.Fail($"Did not reach success PC ${SuccessPC:X4} after {MaxSteps} steps; last PC=${cpu.PC:X4}");
    }
}
