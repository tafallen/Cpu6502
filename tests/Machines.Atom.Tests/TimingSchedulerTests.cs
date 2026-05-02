using Machines.Common;

namespace Machines.Atom.Tests;

public class TimingSchedulerTests
{
    [Fact]
    public void RunDue_ExecutesCallbacksInCycleOrder()
    {
        var clock = new MachineClock();
        var scheduler = new TimingScheduler(clock);
        var calls = new List<int>();

        scheduler.ScheduleAt(30, () => calls.Add(3));
        scheduler.ScheduleAt(10, () => calls.Add(1));
        scheduler.ScheduleAt(20, () => calls.Add(2));

        scheduler.RunDue(20);

        Assert.Equal([1, 2], calls);
    }

    [Fact]
    public void ScheduleIn_UsesCurrentClockAsBase()
    {
        var clock = new MachineClock();
        var scheduler = new TimingScheduler(clock);
        bool fired = false;

        clock.Advance(50);
        scheduler.ScheduleIn(10, () => fired = true);

        scheduler.RunDue(59);
        Assert.False(fired);

        scheduler.RunDue(60);
        Assert.True(fired);
    }

    [Fact]
    public void RunDue_PreservesFifoOrder_ForSameCycleCallbacks()
    {
        var clock = new MachineClock();
        var scheduler = new TimingScheduler(clock);
        var calls = new List<int>();

        scheduler.ScheduleAt(10, () => calls.Add(1));
        scheduler.ScheduleAt(10, () => calls.Add(2));
        scheduler.ScheduleAt(10, () => calls.Add(3));

        scheduler.RunDue(10);

        Assert.Equal([1, 2, 3], calls);
    }

    [Fact]
    public void RunDue_ExecutesCallbacksScheduledForNow_FromInsideCallback()
    {
        var clock = new MachineClock();
        var scheduler = new TimingScheduler(clock);
        var calls = new List<int>();

        scheduler.ScheduleAt(10, () =>
        {
            calls.Add(1);
            scheduler.ScheduleAt(10, () => calls.Add(2));
        });

        scheduler.RunDue(10);

        Assert.Equal([1, 2], calls);
    }
}

