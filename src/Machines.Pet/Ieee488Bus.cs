using Cpu6502.Core;

namespace Machines.Pet;

/// <summary>
/// IEEE-488 (GPIB) Parallel Bus Interface emulation for Commodore PET.
/// Manages DAV (Data Valid), NRFD (Not Ready For Data), NDAC (Not Data Accepted),
/// ATN (Attention), and 8-bit parallel DIO line transactions.
/// </summary>
public sealed class Ieee488Bus
{
    public bool AtnLine { get; private set; }
    public bool DavLine { get; private set; }
    public bool NrfdLine { get; private set; }
    public bool NdacLine { get; private set; }
    public byte DataBus { get; private set; }

    public void AssertAttention(bool active) => AtnLine = active;

    public void WriteData(byte data)
    {
        DataBus = data;
        DavLine = true;
    }

    public void AcknowledgeData()
    {
        NdacLine = false;
        NrfdLine = true;
    }

    public void AutoLoadPrg(byte[] prgData, Ram ram)
    {
        if (prgData.Length < 2) return;

        ushort loadAddress = (ushort)(prgData[0] | (prgData[1] << 8));
        byte[] payload = prgData.AsSpan(2).ToArray();

        ram.Load(loadAddress, payload);
    }
}
