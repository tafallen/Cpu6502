namespace Cpu6502.Core;

/// <summary>
/// Execution frame capturing CPU state at a single instruction step.
/// </summary>
public readonly record struct ExecutionFrame(
    ushort PC,
    byte Opcode,
    byte A,
    byte X,
    byte Y,
    byte SP,
    byte P,
    ulong Cycles
);

/// <summary>
/// Fixed-capacity circular ring buffer that maintains recent CPU instruction history.
/// Useful for reverse debugging, execution trace logging, and crash analysis.
/// </summary>
public sealed class InstructionHistoryBuffer
{
    private readonly ExecutionFrame[] _buffer;
    private int _head;
    private int _count;

    public InstructionHistoryBuffer(int capacity = 1024)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        _buffer = new ExecutionFrame[capacity];
    }

    public int Capacity => _buffer.Length;
    public int Count => _count;

    public void Record(ushort pc, byte opcode, byte a, byte x, byte y, byte sp, byte p, ulong cycles)
    {
        _buffer[_head] = new ExecutionFrame(pc, opcode, a, x, y, sp, p, cycles);
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length)
            _count++;
    }

    public IReadOnlyList<ExecutionFrame> GetHistory()
    {
        var result = new List<ExecutionFrame>(_count);
        int start = (_head - _count + _buffer.Length) % _buffer.Length;
        for (int i = 0; i < _count; i++)
        {
            result.Add(_buffer[(start + i) % _buffer.Length]);
        }
        return result;
    }
}
