using Cpu6502.Core;
using System.Text.Json;

namespace Cpu6502.Tests;

/// <summary>
/// Test implementation of IExecutionTrace that records all execution events.
/// Used for validating instruction tracing, memory access logging, and interrupt handling.
/// </summary>
public sealed class RecordingTrace : IExecutionTrace
{
    /// <summary>Recorded instruction execution event with full state snapshot.</summary>
    public sealed record InstructionEvent(
        ushort Pc,
        byte Opcode,
        int Cycles,
        byte AAfter,
        byte Flags
    );

    /// <summary>Recorded memory access event.</summary>
    public sealed record MemoryAccessEvent(
        ushort Address,
        byte Value,
        bool IsWrite
    );

    /// <summary>Recorded interrupt event with resolved handler address.</summary>
    public sealed record InterruptEvent(
        InterruptType Type,
        ushort HandlerAddress
    );

    /// <summary>All instruction execution events, in order.</summary>
    public List<InstructionEvent> Instructions { get; } = new();

    /// <summary>All memory access events (reads and writes), in order.</summary>
    public List<MemoryAccessEvent> MemoryAccesses { get; } = new();

    /// <summary>All interrupt events, in order.</summary>
    public List<InterruptEvent> Interrupts { get; } = new();

    public void OnInstructionFetched(ushort pc, byte opcode)
    {
        // Fetch events are not recorded; execution event captures full state
    }

    public void OnInstructionExecuted(ushort pc, byte opcode, int cycles, byte aAfter, byte flags)
    {
        Instructions.Add(new(pc, opcode, cycles, aAfter, flags));
    }

    public void OnMemoryAccess(ushort address, byte value, bool isWrite)
    {
        MemoryAccesses.Add(new(address, value, isWrite));
    }

    public void OnInterrupt(InterruptType type, ushort handlerAddress)
    {
        Interrupts.Add(new(type, handlerAddress));
    }

    /// <summary>Export all recorded events as JSON string for analysis/debugging.</summary>
    public string ExportJson()
    {
        var export = new
        {
            Instructions = Instructions.Select(i => new
            {
                i.Pc,
                i.Opcode,
                i.Cycles,
                i.AAfter,
                i.Flags
            }).ToList(),
            MemoryAccesses = MemoryAccesses.Select(m => new
            {
                m.Address,
                m.Value,
                m.IsWrite
            }).ToList(),
            Interrupts = Interrupts.Select(intr => new
            {
                intr.Type,
                intr.HandlerAddress
            }).ToList()
        };

        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Clear all recorded events.</summary>
    public void Clear()
    {
        Instructions.Clear();
        MemoryAccesses.Clear();
        Interrupts.Clear();
    }
}
