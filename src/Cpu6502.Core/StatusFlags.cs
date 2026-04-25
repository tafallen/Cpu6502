namespace Cpu6502.Core;

[Flags]
public enum StatusFlags : byte
{
    Carry            = 0x01,
    Zero             = 0x02,
    InterruptDisable = 0x04,
    Decimal          = 0x08,
    Break            = 0x10,
    Unused           = 0x20,  // Always reads as 1
    Overflow         = 0x40,
    Negative         = 0x80,
}
