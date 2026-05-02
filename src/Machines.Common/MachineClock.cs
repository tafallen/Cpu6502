namespace Machines.Common;

public interface IClock
{
    ulong Now { get; }
    void Advance(int cycles);
    void Set(ulong cycle);
}

public sealed class MachineClock : IClock
{
    public ulong Now { get; private set; }

    public void Advance(int cycles)
    {
        if (cycles < 0)
            throw new ArgumentOutOfRangeException(nameof(cycles), "Cycle delta must be non-negative.");

        Now += (ulong)cycles;
    }

    public void Set(ulong cycle) => Now = cycle;
}

