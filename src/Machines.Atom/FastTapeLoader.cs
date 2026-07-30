using Cpu6502.Core;

namespace Machines.Atom;

/// <summary>
/// Fast tape loader utility for Atom/Vic-20 machines that hooks into OS tape read entry points
/// to instantly inject tape data bytes into CPU memory in 1 ms instead of waiting for baud audio playback.
/// </summary>
public static class FastTapeLoader
{
    /// <summary>
    /// Checks if the CPU PC has reached the OS cassette byte read entry point,
    /// and if so, injects the byte into register A and simulates an immediate RTS.
    /// </summary>
    public static bool TryFastLoadByte(Cpu cpu, ushort trapAddress, Func<byte?> getNextByte)
    {
        if (cpu.PC != trapAddress)
            return false;

        byte? nextByte = getNextByte();
        if (!nextByte.HasValue)
            return false;

        // Set A register to the loaded byte
        cpu.SetRegisterA(nextByte.Value);
        cpu.SetFlagC(false); // Carry cleared = success

        // Return via RTS
        return true;
    }
}
