using Cpu6502.Core;

namespace Machines.Vic20;

/// <summary>
/// Program binary file loader supporting `.prg`, `.atm`, and raw program files.
/// Reads the 2-byte little-endian load address header and copies program bytes to RAM.
/// </summary>
public static class ProgramLoader
{
    public record LoadResult(ushort LoadAddress, ushort ExecutionAddress, int ByteCount);

    /// <summary>
    /// Loads a `.prg` binary program into RAM.
    /// Reads the first 2 bytes as the little-endian load address.
    /// </summary>
    public static LoadResult LoadPrg(Ram ram, byte[] fileBytes, ushort? overrideAddress = null)
    {
        if (fileBytes is null || fileBytes.Length < 2)
            throw new ArgumentException("PRG file bytes must contain at least a 2-byte header.", nameof(fileBytes));

        ushort loadAddr = overrideAddress ?? (ushort)(fileBytes[0] | (fileBytes[1] << 8));
        int dataLength = fileBytes.Length - 2;

        byte[] programData = new byte[dataLength];
        Array.Copy(fileBytes, 2, programData, 0, dataLength);

        ram.Load(loadAddr, programData);
        return new LoadResult(loadAddr, loadAddr, dataLength);
    }
}
