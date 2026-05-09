using System;
using System.Collections.Generic;
using Cpu6502.Core;

namespace Adapters.Gdb;

/// <summary>
/// Wrapper trace that adds GDB breakpoint checking on top of an existing trace.
/// Allows GDB breakpoints to coexist with other trace implementations.
/// </summary>
internal sealed class BreakpointWrapperTrace : IExecutionTrace
{
    private readonly IExecutionTrace _innerTrace;
    private readonly HashSet<ushort> _breakpoints;

    public int MemoryAccessSampleRate => _innerTrace.MemoryAccessSampleRate;

    public BreakpointWrapperTrace(IExecutionTrace innerTrace, HashSet<ushort> breakpoints)
    {
        _innerTrace = innerTrace ?? NullTrace.Instance;
        _breakpoints = breakpoints ?? new HashSet<ushort>();
    }

    public void OnInstructionFetched(ushort pc, byte opcode)
    {
        _innerTrace.OnInstructionFetched(pc, opcode);
    }

    public void OnInstructionExecuted(ushort pc, byte opcode, int cycles, byte aAfter, byte flags)
    {
        _innerTrace.OnInstructionExecuted(pc, opcode, cycles, aAfter, flags);
    }

    public void OnMemoryAccess(ushort address, byte value, bool isWrite, ulong cycles = 0)
    {
        _innerTrace.OnMemoryAccess(address, value, isWrite, cycles);
    }

    public void OnInterrupt(InterruptType type, ushort handlerAddress)
    {
        _innerTrace.OnInterrupt(type, handlerAddress);
    }

    public bool ShouldBreak(ushort pc, byte opcode, byte a)
    {
        // Check GDB breakpoints first
        if (_breakpoints.Contains(pc))
            return true;
        
        // Fall through to inner trace
        return _innerTrace.ShouldBreak(pc, opcode, a);
    }

    public bool ShouldRecordMemoryAccess(ushort address, bool isWrite)
    {
        return _innerTrace.ShouldRecordMemoryAccess(address, isWrite);
    }
}
