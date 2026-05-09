using System;
using System.Collections.Generic;
using System.Linq;
using Cpu6502.Core;

namespace Adapters.Gdb;

/// <summary>
/// GDB target adapter for 6502 CPU.
/// Maps CPU state to GDB registers and memory access.
/// Register mapping:
///   0: A (accumulator)
///   1: X (index)
///   2: Y (index)
///   3: SP (stack pointer)
///   4: PC low byte
///   5: PC high byte
///   6-11: Flags (C, Z, I, D, V, N) as individual boolean bytes
///   12: Unused
/// </summary>
public sealed class Cpu6502GdbTarget : IGdbTarget
{
    private readonly Cpu _cpu;
    private readonly IBus _bus;
    private readonly HashSet<ushort> _breakpoints = new();
    private volatile bool _halted;
    private bool _hitBreakpoint;

    public bool IsHalted => _halted;

    public Cpu6502GdbTarget(Cpu cpu, IBus bus)
    {
        _cpu = cpu ?? throw new ArgumentNullException(nameof(cpu));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    public string GetHaltReason()
    {
        if (_hitBreakpoint)
        {
            _hitBreakpoint = false;
            return "S05";  // SIGTRAP
        }
        return "S00";  // No signal
    }

    public string ReadAllRegisters()
    {
        // Return all registers as hex-encoded bytes
        byte[] regs = new byte[13];
        regs[0] = _cpu.A;
        regs[1] = _cpu.X;
        regs[2] = _cpu.Y;
        regs[3] = _cpu.SP;
        regs[4] = (byte)(_cpu.PC & 0xFF);
        regs[5] = (byte)((_cpu.PC >> 8) & 0xFF);
        regs[6] = _cpu.C ? (byte)1 : (byte)0;
        regs[7] = _cpu.Z ? (byte)1 : (byte)0;
        regs[8] = _cpu.I ? (byte)1 : (byte)0;
        regs[9] = _cpu.D ? (byte)1 : (byte)0;
        regs[10] = _cpu.V ? (byte)1 : (byte)0;
        regs[11] = _cpu.N ? (byte)1 : (byte)0;
        regs[12] = 0;  // Unused register

        return BytesToHex(regs);
    }

    public void WriteAllRegisters(string hexData)
    {
        byte[] regs = HexToBytes(hexData);
        if (regs.Length < 12)
            throw new ArgumentException("Not enough register data", nameof(hexData));

        _cpu.SetRegisterA(regs[0]);
        _cpu.SetRegisterX(regs[1]);
        _cpu.SetRegisterY(regs[2]);
        _cpu.SetStackPointer(regs[3]);
        
        ushort pc = (ushort)(regs[4] | (regs[5] << 8));
        _cpu.SetProgramCounter(pc);

        _cpu.SetFlagC(regs[6] != 0);
        _cpu.SetFlagZ(regs[7] != 0);
        _cpu.SetFlagI(regs[8] != 0);
        _cpu.SetFlagD(regs[9] != 0);
        _cpu.SetFlagV(regs[10] != 0);
        _cpu.SetFlagN(regs[11] != 0);
    }

    public string ReadRegister(int regNum)
    {
        byte val = regNum switch
        {
            0 => _cpu.A,
            1 => _cpu.X,
            2 => _cpu.Y,
            3 => _cpu.SP,
            4 => (byte)(_cpu.PC & 0xFF),
            5 => (byte)((_cpu.PC >> 8) & 0xFF),
            6 => _cpu.C ? (byte)1 : (byte)0,
            7 => _cpu.Z ? (byte)1 : (byte)0,
            8 => _cpu.I ? (byte)1 : (byte)0,
            9 => _cpu.D ? (byte)1 : (byte)0,
            10 => _cpu.V ? (byte)1 : (byte)0,
            11 => _cpu.N ? (byte)1 : (byte)0,
            _ => throw new ArgumentOutOfRangeException(nameof(regNum), "Invalid register number")
        };
        return val.ToString("X2");
    }

    public void WriteRegister(int regNum, string hexData)
    {
        byte val = byte.Parse(hexData, System.Globalization.NumberStyles.HexNumber);

        switch (regNum)
        {
            case 0: _cpu.SetRegisterA(val); break;
            case 1: _cpu.SetRegisterX(val); break;
            case 2: _cpu.SetRegisterY(val); break;
            case 3: _cpu.SetStackPointer(val); break;
            case 4: _cpu.SetProgramCounter((ushort)((_cpu.PC & 0xFF00) | val)); break;
            case 5: _cpu.SetProgramCounter((ushort)((_cpu.PC & 0x00FF) | (val << 8))); break;
            case 6: _cpu.SetFlagC(val != 0); break;
            case 7: _cpu.SetFlagZ(val != 0); break;
            case 8: _cpu.SetFlagI(val != 0); break;
            case 9: _cpu.SetFlagD(val != 0); break;
            case 10: _cpu.SetFlagV(val != 0); break;
            case 11: _cpu.SetFlagN(val != 0); break;
            default: throw new ArgumentOutOfRangeException(nameof(regNum));
        }
    }

    public string ReadMemory(ushort address, int length)
    {
        byte[] data = new byte[length];
        for (int i = 0; i < length; i++)
            data[i] = _bus.Read((ushort)(address + i));
        return BytesToHex(data);
    }

    public void WriteMemory(ushort address, string hexData)
    {
        byte[] data = HexToBytes(hexData);
        for (int i = 0; i < data.Length; i++)
            _bus.Write((ushort)(address + i), data[i]);
    }

    public ushort GetProgramCounter() => _cpu.PC;

    public void SetProgramCounter(ushort pc) => _cpu.SetProgramCounter(pc);

    public bool Step()
    {
        try
        {
            _cpu.Step();
        }
        catch (BreakException)
        {
            _hitBreakpoint = true;
            _halted = true;
            return true;
        }
        return false;
    }

    public void Continue()
    {
        _halted = false;
        // Note: This would need to run in a background thread for non-blocking behavior
        // For now, this is a placeholder
    }

    public void Pause()
    {
        _halted = true;
    }

    public void SetBreakpoint(ushort address)
    {
        _breakpoints.Add(address);
        UpdateTraceBreakpointCondition();
    }

    public void RemoveBreakpoint(ushort address)
    {
        _breakpoints.Remove(address);
        UpdateTraceBreakpointCondition();
    }

    private void UpdateTraceBreakpointCondition()
    {
        // Wrap current trace to include GDB breakpoints in the ShouldBreak check
        var currentTrace = _cpu.Trace ?? NullTrace.Instance;
        
        // Create a wrapper trace that adds GDB breakpoint checks
        _cpu.Trace = new BreakpointWrapperTrace(currentTrace, _breakpoints);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private static string BytesToHex(byte[] data)
    {
        return string.Concat(data.Select(b => b.ToString("X2")));
    }

    private static byte[] HexToBytes(string hex)
    {
        byte[] data = new byte[hex.Length / 2];
        for (int i = 0; i < data.Length; i++)
            data[i] = byte.Parse(hex[(i * 2)..(i * 2 + 2)], System.Globalization.NumberStyles.HexNumber);
        return data;
    }
}
