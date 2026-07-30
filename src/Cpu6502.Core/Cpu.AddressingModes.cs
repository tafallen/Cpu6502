using System.Runtime.CompilerServices;

namespace Cpu6502.Core;

public sealed partial class Cpu
{
    // ── Addressing mode helpers ───────────────────────────────────────────────
    // Each returns the effective address. Cycle penalties are applied inline.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AddrImmediate()
    {
        return PC++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AddrZeroPage()
    {
        return Fetch();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AddrZeroPageX()
    {
        return (byte)(Fetch() + X);   // wraps within zero page
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AddrZeroPageY()
    {
        return (byte)(Fetch() + Y);   // wraps within zero page
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AddrAbsolute()
    {
        byte lo = Fetch();
        byte hi = Fetch();
        return (ushort)((hi << 8) | lo);
    }

    /// <summary>Absolute,X with optional +1 cycle for page cross.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AddrAbsoluteX(bool alwaysAddCycle = false)
    {
        ushort base_ = AddrAbsolute();
        ushort addr  = (ushort)(base_ + X);
        ApplyPageCrossPenalty(base_, addr, alwaysAddCycle);
        return addr;
    }

    /// <summary>Absolute,Y with optional +1 cycle for page cross.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AddrAbsoluteY(bool alwaysAddCycle = false)
    {
        ushort base_ = AddrAbsolute();
        ushort addr  = (ushort)(base_ + Y);
        ApplyPageCrossPenalty(base_, addr, alwaysAddCycle);
        return addr;
    }

    /// <summary>(Indirect,X) — pre-indexed indirect through zero page.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AddrIndexedIndirect()
    {
        byte ptr = (byte)(Fetch() + X);
        return ReadWordBug(ptr);
    }

    /// <summary>(Indirect),Y — post-indexed indirect with optional +1 cycle for page cross.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AddrIndirectIndexed(bool alwaysAddCycle = false)
    {
        byte   zpAddr = Fetch();
        ushort base_  = ReadWordBug(zpAddr);
        ushort addr   = (ushort)(base_ + Y);
        ApplyPageCrossPenalty(base_, addr, alwaysAddCycle);
        return addr;
    }
}
