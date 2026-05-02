namespace Machines.Common;

public interface ITimingScheduler
{
    void ScheduleAt(ulong cycle, Action callback);
    void ScheduleIn(int deltaCycles, Action callback);
    void RunDue(ulong now);
}

public sealed class TimingScheduler : ITimingScheduler
{
    private readonly IClock _clock;
    private readonly PriorityQueue<ScheduledCallback, (ulong Cycle, long Order)> _queue = new();
    private long _order;

    public TimingScheduler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public void ScheduleAt(ulong cycle, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _queue.Enqueue(new ScheduledCallback(callback), (cycle, _order++));
    }

    public void ScheduleIn(int deltaCycles, Action callback)
    {
        if (deltaCycles < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaCycles), "Cycle delta must be non-negative.");

        ScheduleAt(_clock.Now + (ulong)deltaCycles, callback);
    }

    public void RunDue(ulong now)
    {
        while (_queue.TryPeek(out _, out var priority) && priority.Cycle <= now)
        {
            var scheduled = _queue.Dequeue();
            scheduled.Callback();
        }
    }

    private readonly record struct ScheduledCallback(Action Callback);
}

