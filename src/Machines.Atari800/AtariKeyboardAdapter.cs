namespace Machines.Atari800;

/// <summary>
/// Atari 800XL 64-key keyboard matrix scanner adapter.
/// Connects matrix key codes to POKEY KBCODE register ($D209).
/// </summary>
public sealed class AtariKeyboardAdapter
{
    private byte _currentKeyCode = 0xFF;

    public byte ReadKeyCode() => _currentKeyCode;

    public void KeyDown(byte keyCode)
    {
        _currentKeyCode = keyCode;
    }

    public void KeyUp(byte keyCode)
    {
        if (_currentKeyCode == keyCode)
        {
            _currentKeyCode = 0xFF;
        }
    }
}
