using Cpu6502.Core;

namespace Machines.BbcMaster;

/// <summary>
/// Acorn BBC Master 128 ACCCON Access Control Register ($FE34).
/// High-performance implementation with cached boolean flags.
/// </summary>
public sealed class BbcMasterAcccon : IBus
{
    private byte _value;
    private bool _mainRamSelect = true;
    private bool _displayShadowSelect;
    private bool _executeShadowSelect;
    private bool _hazelSelect;

    public byte Value => _value;

    public bool MainRamSelect => _mainRamSelect;
    public bool DisplayShadowSelect => _displayShadowSelect;
    public bool ExecuteShadowSelect => _executeShadowSelect;
    public bool HazelSelect => _hazelSelect;

    public byte Read(ushort address) => _value;

    public void Write(ushort address, byte value)
    {
        _value = value;
        _mainRamSelect = (value & 0x01) == 0;
        _displayShadowSelect = (value & 0x02) != 0;
        _executeShadowSelect = (value & 0x04) != 0;
        _hazelSelect = (value & 0x08) != 0;
    }
}
