using Adapters.Gdb;
using Cpu6502.Core;

namespace Adapters.Gdb.Tests;

public sealed class GdbAdapterTests
{
    [Fact]
    public void Constructor_NullCpu_Throws()
    {
        var bus = new Ram(0x10000);

        Assert.Throws<ArgumentNullException>(() => new Cpu6502GdbTarget(null!, bus));
    }

    [Fact]
    public void Constructor_NullBus_Throws()
    {
        var cpu = new Cpu(new Ram(0x10000));

        Assert.Throws<ArgumentNullException>(() => new Cpu6502GdbTarget(cpu, null!));
    }

    [Fact]
    public void ReadAllRegisters_ReflectsWrittenState()
    {
        var cpu = new Cpu(new Ram(0x10000));
        var target = new Cpu6502GdbTarget(cpu, new Ram(0x10000));

        target.WriteAllRegisters("11223344556601000100010099");

        Assert.Equal("11223344556601000100010000", target.ReadAllRegisters());
        Assert.Equal(0x11, cpu.A);
        Assert.Equal(0x22, cpu.X);
        Assert.Equal(0x33, cpu.Y);
        Assert.Equal(0x44, cpu.SP);
        Assert.Equal(0x6655, cpu.PC);
        Assert.True(cpu.C);
        Assert.False(cpu.Z);
        Assert.True(cpu.I);
        Assert.False(cpu.D);
        Assert.True(cpu.V);
        Assert.False(cpu.N);
    }

    [Theory]
    [InlineData(0, "10")]
    [InlineData(1, "20")]
    [InlineData(2, "30")]
    [InlineData(3, "40")]
    [InlineData(4, "50")]
    [InlineData(5, "60")]
    [InlineData(6, "01")]
    [InlineData(7, "00")]
    [InlineData(8, "01")]
    [InlineData(9, "00")]
    [InlineData(10, "01")]
    [InlineData(11, "00")]
    public void WriteRegister_UpdatesIndividualRegister(int regNum, string value)
    {
        var cpu = new Cpu(new Ram(0x10000));
        var target = new Cpu6502GdbTarget(cpu, new Ram(0x10000));

        target.WriteAllRegisters("00000000123400000000000000");
        target.WriteRegister(regNum, value);

        Assert.Equal(value, target.ReadRegister(regNum));
    }

    [Theory]
    [InlineData(0, "AB")]
    [InlineData(1, "CD")]
    [InlineData(2, "EF")]
    [InlineData(3, "12")]
    [InlineData(4, "34")]
    [InlineData(5, "56")]
    [InlineData(6, "01")]
    [InlineData(7, "01")]
    [InlineData(8, "01")]
    [InlineData(9, "01")]
    [InlineData(10, "01")]
    [InlineData(11, "01")]
    public void ReadRegister_ReturnsEncodedValue(int regNum, string value)
    {
        var cpu = new Cpu(new Ram(0x10000));
        var target = new Cpu6502GdbTarget(cpu, new Ram(0x10000));

        target.WriteAllRegisters("ABCDEF12345601010101010100");

        Assert.Equal(value, target.ReadRegister(regNum));
    }

    [Fact]
    public void InvalidRegisterNumbersThrow()
    {
        var target = new Cpu6502GdbTarget(new Cpu(new Ram(0x10000)), new Ram(0x10000));

        Assert.Throws<ArgumentOutOfRangeException>(() => target.ReadRegister(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => target.ReadRegister(12));
        Assert.Throws<ArgumentOutOfRangeException>(() => target.WriteRegister(-1, "00"));
        Assert.Throws<ArgumentOutOfRangeException>(() => target.WriteRegister(12, "00"));
    }

    [Fact]
    public void ReadAndWriteMemory_UseSequentialAddresses()
    {
        var ram = new Ram(0x10000);
        var target = new Cpu6502GdbTarget(new Cpu(ram), ram);

        ram.Write(0x2000, 0xAA);
        ram.Write(0x2001, 0xBB);
        ram.Write(0x2002, 0xCC);

        Assert.Equal("AABBCC", target.ReadMemory(0x2000, 3));

        target.WriteMemory(0x2100, "010203");

        Assert.Equal(0x01, ram.Read(0x2100));
        Assert.Equal(0x02, ram.Read(0x2101));
        Assert.Equal(0x03, ram.Read(0x2102));
    }

    [Fact]
    public void ProgramCounter_GetAndSet_Work()
    {
        var target = new Cpu6502GdbTarget(new Cpu(new Ram(0x10000)), new Ram(0x10000));

        target.SetProgramCounter(0x4567);

        Assert.Equal(0x4567, target.GetProgramCounter());
    }

    [Fact]
    public void Step_ReturnsFalseForNormalInstruction()
    {
        var ram = new Ram(0x10000);
        var target = new Cpu6502GdbTarget(new Cpu(ram), ram);

        target.SetProgramCounter(0x0200);
        ram.Write(0x0200, 0xEA);

        Assert.False(target.Step());
        Assert.Equal(0x0201, target.GetProgramCounter());
        Assert.False(target.IsHalted);
    }

    [Fact]
    public void Step_HaltsOnBreakpointAndReportsSignal()
    {
        var ram = new Ram(0x10000);
        var target = new Cpu6502GdbTarget(new Cpu(ram), ram);

        target.SetProgramCounter(0x0200);
        ram.Write(0x0200, 0xEA);
        target.SetBreakpoint(0x0200);

        Assert.True(target.Step());
        Assert.True(target.IsHalted);
        Assert.Equal(0x0200, target.GetProgramCounter());
        Assert.Equal("S05", target.GetHaltReason());
        Assert.Equal("S00", target.GetHaltReason());
    }

    [Fact]
    public void SetAndRemoveBreakpoint_PreservesExistingTrace()
    {
        var ram = new Ram(0x10000);
        var cpu = new Cpu(ram);
        var target = new Cpu6502GdbTarget(cpu, ram);
        cpu.Trace = new BreakOnAddressTrace(0x0300);

        target.SetProgramCounter(0x0200);
        ram.Write(0x0200, 0xEA);
        target.SetBreakpoint(0x0200);

        Assert.True(target.Step());
        Assert.True(target.IsHalted);

        target.RemoveBreakpoint(0x0200);
        target.Continue();
        target.SetProgramCounter(0x0300);
        ram.Write(0x0300, 0xEA);

        Assert.True(target.Step());
    }

    [Fact]
    public void PauseAndContinue_ToggleHaltedState()
    {
        var target = new Cpu6502GdbTarget(new Cpu(new Ram(0x10000)), new Ram(0x10000));

        target.Pause();
        Assert.True(target.IsHalted);

        target.Continue();
        Assert.False(target.IsHalted);
    }

    [Fact]
    public void RspPacket_EncodeAndParseRoundTrip()
    {
        var packet = new RspPacket("g");

        Assert.Equal("$g#67", packet.Encode());
        Assert.Equal('g', RspPacket.Parse(packet.Encode()).Command);
    }

    [Fact]
    public void RspPacket_ParseRejectsBadChecksum()
    {
        var ex = Assert.Throws<ArgumentException>(() => RspPacket.Parse("$g#00"));

        Assert.Contains("Checksum mismatch", ex.Message);
    }

    private sealed class BreakOnAddressTrace : IExecutionTrace
    {
        private readonly ushort _breakAddress;

        public BreakOnAddressTrace(ushort breakAddress)
        {
            _breakAddress = breakAddress;
        }

        public void OnInstructionFetched(ushort pc, byte opcode)
        {
        }

        public void OnInstructionExecuted(ushort pc, byte opcode, int cycles, byte aAfter, byte flags)
        {
        }

        public void OnMemoryAccess(ushort address, byte value, bool isWrite, ulong cycles)
        {
        }

        public void OnInterrupt(InterruptType type, ushort handlerAddress)
        {
        }

        public bool ShouldBreak(ushort pc, byte opcode, byte currentA) => pc == _breakAddress;
    }
}
