namespace Machines.Common;

/// <summary>
/// Detects rising edges on interrupt lines for edge-triggered interrupt semantics.
///
/// 6502 interrupt pins are level-sensitive (IRQ and NMI inputs are held HIGH to request an interrupt),
/// but the CPU requires edge-triggered semantics: an interrupt is only serviced once per edge, not held.
/// This component encapsulates the edge-detection logic needed to convert level-sensitive hardware
/// into the edge-triggered behavior the CPU expects.
///
/// Usage in machine timing loops:
///   private readonly InterruptEdgeDetector _irqEdge = new();
///   
///   // In RunFrame() or Step()
///   if (_irqEdge.Detect(via1.IrqActive))
///       cpu.Irq();
/// </summary>
public sealed class InterruptEdgeDetector
{
    private bool _lineWasActive;

    /// <summary>
    /// Detect a rising edge on the interrupt line.
    /// Returns true only when the line transitions from inactive (false) to active (true).
    /// Falls back to false, level-high with no change, and rising edges after a fall all return appropriately.
    /// </summary>
    /// <param name="lineActive">Current state of the interrupt line (true = active, false = inactive)</param>
    /// <returns>True if a rising edge was detected (line went from false to true), false otherwise</returns>
    public bool Detect(bool lineActive)
    {
        bool edge = lineActive && !_lineWasActive;
        _lineWasActive = lineActive;
        return edge;
    }
}
