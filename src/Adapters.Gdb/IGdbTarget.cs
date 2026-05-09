namespace Adapters.Gdb;

/// <summary>
/// Abstraction over CPU state for GDB debugging.
/// Maps CPU registers and memory to GDB protocol.
/// </summary>
public interface IGdbTarget
{
    /// <summary>Read all registers as hex-encoded bytes (big-endian).</summary>
    string ReadAllRegisters();

    /// <summary>Write all registers from hex-encoded bytes (big-endian).</summary>
    void WriteAllRegisters(string hexData);

    /// <summary>Read single register (0=A, 1=X, 2=Y, 3=SP_LO, 4=SP_HI, 5=PC_LO, 6=PC_HI, 7-12=flags).</summary>
    string ReadRegister(int regNum);

    /// <summary>Write single register (same numbering as ReadRegister).</summary>
    void WriteRegister(int regNum, string hexData);

    /// <summary>Read memory at address for length bytes (hex-encoded).</summary>
    string ReadMemory(ushort address, int length);

    /// <summary>Write memory at address with hex-encoded data.</summary>
    void WriteMemory(ushort address, string hexData);

    /// <summary>Get program counter (current instruction address).</summary>
    ushort GetProgramCounter();

    /// <summary>Set program counter.</summary>
    void SetProgramCounter(ushort pc);

    /// <summary>Step one instruction (returns true if breakpoint hit).</summary>
    bool Step();

    /// <summary>Continue execution until breakpoint (non-blocking; returns immediately).</summary>
    void Continue();

    /// <summary>Pause execution (for continue-until-breakpoint).</summary>
    void Pause();

    /// <summary>Set breakpoint at address.</summary>
    void SetBreakpoint(ushort address);

    /// <summary>Remove breakpoint at address.</summary>
    void RemoveBreakpoint(ushort address);

    /// <summary>Check if currently halted at breakpoint.</summary>
    bool IsHalted { get; }

    /// <summary>Get halt reason (SIGTRAP for breakpoint, etc).</summary>
    string GetHaltReason();
}
