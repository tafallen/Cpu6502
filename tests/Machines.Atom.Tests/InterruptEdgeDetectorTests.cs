using Machines.Common;
using Xunit;

namespace Machines.Atom.Tests;

public class InterruptEdgeDetectorTests
{
    [Fact]
    public void Detect_LineGoesLowToHigh_ReturnsTrue()
    {
        var detector = new InterruptEdgeDetector();
        
        // First call: line is low (inactive)
        bool edge1 = detector.Detect(false);
        Assert.False(edge1);
        
        // Second call: line goes high (active) → rising edge
        bool edge2 = detector.Detect(true);
        Assert.True(edge2);
    }

    [Fact]
    public void Detect_LineGoesHighToLow_ReturnsFalse()
    {
        var detector = new InterruptEdgeDetector();
        
        // Initialize: line is low (establish baseline state)
        detector.Detect(false);
        
        // First transition: line goes high (rising edge)
        bool edge1 = detector.Detect(true);
        Assert.True(edge1);
        
        // Second transition: line goes low (falling edge, not rising)
        bool edge2 = detector.Detect(false);
        Assert.False(edge2);
    }

    [Fact]
    public void Detect_LineStaysHigh_ReturnsFalse()
    {
        var detector = new InterruptEdgeDetector();
        
        // Initialize: line is low (establish baseline state)
        detector.Detect(false);
        
        // Transition: line goes high
        bool edge1 = detector.Detect(true);
        Assert.True(edge1);
        
        // Stay high (no edge)
        bool edge2 = detector.Detect(true);
        Assert.False(edge2);
        
        // Still high
        bool edge3 = detector.Detect(true);
        Assert.False(edge3);
    }

    [Fact]
    public void Detect_LineStaysLow_ReturnsFalse()
    {
        var detector = new InterruptEdgeDetector();
        
        // First call: line is low
        bool edge1 = detector.Detect(false);
        Assert.False(edge1);
        
        // Second call: line stays low (no edge)
        bool edge2 = detector.Detect(false);
        Assert.False(edge2);
    }

    [Fact]
    public void Detect_MultipleEdges_OnlyFirsRisingEdgeDetected()
    {
        var detector = new InterruptEdgeDetector();
        
        // Start low
        detector.Detect(false);
        
        // Rising edge
        bool edge1 = detector.Detect(true);
        Assert.True(edge1);
        
        // Stays high, no edge
        bool edge2 = detector.Detect(true);
        Assert.False(edge2);
        
        // Falls low
        bool edge3 = detector.Detect(false);
        Assert.False(edge3);
        
        // Rises again
        bool edge4 = detector.Detect(true);
        Assert.True(edge4);
    }

    [Fact]
    public void Detect_EdgeStatePersistsAcrossCalls()
    {
        var detector = new InterruptEdgeDetector();
        
        // Sequence: low → high (rising edge detected)
        detector.Detect(false);
        bool edge1 = detector.Detect(true);
        Assert.True(edge1);
        
        // Much later, call again with same state
        bool edge2 = detector.Detect(true);
        Assert.False(edge2, "State should persist; no new edge if line stays high");
    }
}
