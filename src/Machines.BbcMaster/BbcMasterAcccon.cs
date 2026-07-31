using Cpu6502.Core;

namespace Machines.BbcMaster;

/// <summary>
/// Acorn BBC Master 128 ACCCON Access Control Register ($FE34).
/// Controls Shadow RAM banking, HAZEL private RAM ($8000–$9FFF), and Turbo mode.
/// Bit 0: D (0 = Main RAM, 1 = Shadow RAM at $0000–$7FFF)
/// Bit 1: E (0 = Display reads Main RAM, 1 = Display reads Shadow RAM)
/// Bit 2: X (Execute from Shadow RAM)
/// Bit 3: Y (1 = HAZEL RAM at $8000–$9FFF)
/// Bit 7: T (Turbo 2 MHz speed)
/// </summary>
public sealed class BbcMasterAcccon : IBus
{
    public byte Value { get; private set; }

    public bool MainRamSelect => (Value & 0x01) == 0;
    public bool DisplayShadowSelect => (Value & 0x02) != 0;
    public bool ExecuteShadowSelect => (Value & 0x04) != 0;
    public bool HazelSelect => (Value & 0x08) != 0;

    public byte Read(ushort address) => Value;

    public void Write(ushort address, byte value)
    {
        Value = value;
    }
}
