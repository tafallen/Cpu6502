using Cpu6502.Core;

namespace Machines.BbcMicro;

/// <summary>
/// BBC Micro Model B 16-bank Sideways ROM bank selector ($8000–$BFFF).
/// On the BBC Micro, writing the bank number (0–15) to IC32 latch ($FE30)
/// selects which 16 KB ROM bank occupies the $8000–$BFFF address window.
/// Bank 15 is standard BBC BASIC II; Bank 14/13 are DFS and OS System ROMs.
/// Unpopulated banks return 0xFF (open bus).
/// </summary>
public sealed class BbcSidewaysRomBank : IBus
{
    private readonly byte[][] _romBanks = new byte[16][];
    private byte _currentBank = 15; // default to Bank 15 (BBC BASIC)

    public byte ActiveBank => _currentBank;

    public void SelectBank(byte bank)
    {
        _currentBank = (byte)(bank & 0x0F);
    }

    public void SetBankRom(byte bank, byte[] romData)
    {
        byte bankIdx = (byte)(bank & 0x0F);
        byte[] copy = new byte[0x4000];
        Array.Fill(copy, (byte)0xFF);
        int bytesToCopy = Math.Min(romData.Length, 0x4000);
        Array.Copy(romData, 0, copy, 0, bytesToCopy);
        _romBanks[bankIdx] = copy;
    }

    public byte Read(ushort address)
    {
        ushort offset = (ushort)(address & 0x3FFF);
        byte[]? bank = _romBanks[_currentBank];
        if (bank is not null)
            return bank[offset];
        return 0xFF; // open bus
    }

    public void Write(ushort address, byte value)
    {
        // Sideways ROM window ignores CPU writes
    }
}
