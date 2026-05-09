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
        bool IsWrite,
        ulong Cycles  // Cycle count at time of access
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

    /// <summary>Optional address range filter: only record accesses in this range (null = no filter).</summary>
    public (ushort Min, ushort Max)? AddressRangeFilter { get; set; }

     /// <summary>Optional filter to record only writes (null = no filter).</summary>
    public bool? WriteOnlyFilter { get; set; }

    /// <summary>Sample rate for memory accesses (1 = all, 2 = every 2nd, etc).</summary>
    public int MemoryAccessSampleRate { get; set; } = 1;

    /// <summary>Optional breakpoint condition: if set and returns true, triggers breakpoint.</summary>
    public Func<ushort, byte, byte, bool>? BreakpointCondition { get; set; }

    /// <summary>Recorded breakpoint hits during execution.</summary>
    public List<(ushort Pc, byte Opcode, byte AValue)> BreakpointHits { get; } = new();

    public void OnInstructionFetched(ushort pc, byte opcode)
    {
        // Fetch events are not recorded; execution event captures full state
    }

    public bool ShouldRecordMemoryAccess(ushort address, bool isWrite)
    {
        // Check address range filter
        if (AddressRangeFilter.HasValue)
        {
            var (min, max) = AddressRangeFilter.Value;
            if (address < min || address > max)
                return false;
        }

        // Check write-only filter
        if (WriteOnlyFilter.HasValue && WriteOnlyFilter.Value && !isWrite)
            return false;

        return true;
    }

    public bool ShouldBreak(ushort pc, byte opcode, byte currentA)
    {
        if (BreakpointCondition == null)
            return false;

        bool shouldBreak = BreakpointCondition(pc, opcode, currentA);
        if (shouldBreak)
            BreakpointHits.Add((pc, opcode, currentA));

        return shouldBreak;
    }

    public void OnInstructionExecuted(ushort pc, byte opcode, int cycles, byte aAfter, byte flags)
    {
        Instructions.Add(new(pc, opcode, cycles, aAfter, flags));
    }

    public void OnMemoryAccess(ushort address, byte value, bool isWrite, ulong cycles)
    {
        MemoryAccesses.Add(new(address, value, isWrite, cycles));
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
                m.IsWrite,
                m.Cycles
            }).ToList(),
            Interrupts = Interrupts.Select(intr => new
            {
                intr.Type,
                intr.HandlerAddress
            }).ToList()
        };

        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Export instruction trace as CSV format.</summary>
    public string ExportInstructionsCSV()
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("PC,Opcode,Cycles,A,Flags");
        
        foreach (var instr in Instructions)
        {
            csv.AppendLine($"0x{instr.Pc:X4},{instr.Opcode:X2},{instr.Cycles},0x{instr.AAfter:X2},0x{instr.Flags:X2}");
        }
        
        return csv.ToString();
    }

    /// <summary>Export memory access trace as CSV format.</summary>
    public string ExportMemoryAccessesCSV()
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Address,Value,Operation,Cycles");
        
        foreach (var access in MemoryAccesses)
        {
            string op = access.IsWrite ? "WRITE" : "READ";
            csv.AppendLine($"0x{access.Address:X4},0x{access.Value:X2},{op},{access.Cycles}");
        }
        
        return csv.ToString();
    }

    /// <summary>Export instruction trace as binary format (little-endian).</summary>
    public byte[] ExportInstructionsBinary()
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        
        writer.Write(Instructions.Count);
        foreach (var instr in Instructions)
        {
            writer.Write(instr.Pc);
            writer.Write(instr.Opcode);
            writer.Write(instr.Cycles);
            writer.Write(instr.AAfter);
            writer.Write(instr.Flags);
        }
        
        return ms.ToArray();
    }

    /// <summary>Export memory access trace as binary format (little-endian).</summary>
    public byte[] ExportMemoryAccessesBinary()
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        
        writer.Write(MemoryAccesses.Count);
        foreach (var access in MemoryAccesses)
        {
            writer.Write(access.Address);
            writer.Write(access.Value);
            writer.Write(access.IsWrite);
            writer.Write(access.Cycles);
        }
        
        return ms.ToArray();
    }

    /// <summary>Clear all recorded events.</summary>
    public void Clear()
    {
        Instructions.Clear();
        MemoryAccesses.Clear();
        Interrupts.Clear();
    }
}
