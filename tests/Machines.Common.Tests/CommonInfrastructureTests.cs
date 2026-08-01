using Machines.Common;
using Xunit;

namespace Machines.Common.Tests;

public class CommonInfrastructureTests
{
    [Fact]
    public void InterruptEdgeDetector_DetectsRisingAndFallingEdges()
    {
        var detector = new InterruptEdgeDetector();

        Assert.False(detector.Detect(false));
        Assert.True(detector.Detect(true));   // Rising edge
        Assert.False(detector.Detect(true));  // High level
        Assert.False(detector.Detect(false)); // Falling edge
    }

    [Fact]
    public void MachineClock_TicksAndCountsCycles()
    {
        var clock = new MachineClock();
        clock.Advance(500);

        Assert.Equal(500u, clock.Now);
    }

    [Fact]
    public void TimingScheduler_SchedulesAndFiresEvents()
    {
        var clock = new MachineClock();
        var scheduler = new TimingScheduler(clock);
        bool fired = false;

        scheduler.ScheduleIn(100, () => fired = true);
        clock.Advance(50);
        scheduler.RunDue(clock.Now);
        Assert.False(fired);

        clock.Advance(60);
        scheduler.RunDue(clock.Now);
        Assert.True(fired);
    }

    [Fact]
    public void TubeUla_HostAndParasiteRegisterStreaming_Works()
    {
        var tube = new TubeUla();

        // Host writes R1 -> Parasite reads R1
        tube.Write(0x01, 0x42);
        Assert.Equal(0x42, tube.ReadParasite(1));

        // Parasite writes R1 -> Host reads R1
        tube.WriteParasite(1, 0x84);
        Assert.Equal(0x84, tube.Read(0x01));
    }
}
