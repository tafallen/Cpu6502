namespace Machines.Atari800;

public sealed record AtrSector(int SectorNumber, byte[] Data);

/// <summary>
/// Atari 800XL program loader (.xex binary executable format) and .atr floppy disk image parser.
/// Zero-allocation ReadOnlySpan<byte> parser with fast block copying.
/// </summary>
public static class AtariProgramLoader
{
    public static ushort LoadXex(byte[] xexData, AtariBus bus)
    {
        if (xexData.Length < 6)
            throw new ArgumentException("Invalid XEX executable format: too short.");

        ReadOnlySpan<byte> xexSpan = xexData;
        byte[]? ramBuf = bus.Ram.DirectWriteBuffer;
        ushort runAddress = 0;
        int offset = 0;

        while (offset + 4 <= xexSpan.Length)
        {
            ushort header = (ushort)(xexSpan[offset] | (xexSpan[offset + 1] << 8));
            if (header == 0xFFFF)
            {
                offset += 2;
                if (offset + 4 > xexSpan.Length) break;
            }

            ushort startAddr = (ushort)(xexSpan[offset] | (xexSpan[offset + 1] << 8));
            ushort endAddr = (ushort)(xexSpan[offset + 2] | (xexSpan[offset + 3] << 8));
            offset += 4;

            int blockLen = endAddr - startAddr + 1;
            if (blockLen <= 0 || offset + blockLen > xexSpan.Length) break;

            if (ramBuf is not null && startAddr + blockLen <= ramBuf.Length)
            {
                xexSpan.Slice(offset, blockLen).CopyTo(ramBuf.AsSpan(startAddr, blockLen));
            }
            else
            {
                for (int i = 0; i < blockLen; i++)
                {
                    bus.Ram.Write((ushort)(startAddr + i), xexSpan[offset + i]);
                }
            }

            offset += blockLen;
            if (runAddress == 0) runAddress = startAddr;
        }

        return runAddress;
    }

    public static List<AtrSector> ParseAtrSectors(byte[] atrImage)
    {
        var sectors = new List<AtrSector>();
        if (atrImage.Length < 16) return sectors;

        int sectorSize = (atrImage[4] | (atrImage[5] << 8));
        if (sectorSize != 128 && sectorSize != 256) sectorSize = 128;

        int offset = 16;
        int secNum = 1;

        while (offset + sectorSize <= atrImage.Length)
        {
            byte[] secData = new byte[sectorSize];
            Array.Copy(atrImage, offset, secData, 0, sectorSize);
            sectors.Add(new AtrSector(secNum++, secData));
            offset += sectorSize;
        }

        return sectors;
    }
}
