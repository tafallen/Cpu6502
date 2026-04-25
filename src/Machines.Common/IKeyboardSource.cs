namespace Machines.Common;

/// <summary>
/// Provides keyboard matrix state to a chip emulation.
/// The meaning of row/column indices is machine-specific.
/// Implementations translate OS key events into matrix positions.
/// </summary>
public interface IKeyboardSource
{
    bool IsKeyPressed(int row, int column);
}
