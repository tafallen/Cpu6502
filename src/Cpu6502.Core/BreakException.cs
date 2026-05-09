namespace Cpu6502.Core;

/// <summary>
/// Exception thrown when a breakpoint is triggered during execution.
/// Used by IExecutionTrace.ShouldBreak() to pause execution at conditional breakpoints.
/// </summary>
public sealed class BreakException : Exception
{
    /// <summary>Program counter where breakpoint was triggered.</summary>
    public ushort PC { get; }

    /// <summary>Opcode being executed when breakpoint triggered.</summary>
    public byte Opcode { get; }

    /// <summary>Accumulator value when breakpoint triggered.</summary>
    public byte AValue { get; }

    public BreakException(ushort pc, byte opcode, byte aValue)
        : base($"Breakpoint triggered at PC=0x{pc:X4}, Opcode=0x{opcode:X2}, A=0x{aValue:X2}")
    {
        PC = pc;
        Opcode = opcode;
        AValue = aValue;
    }
}
