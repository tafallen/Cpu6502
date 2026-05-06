namespace Cpu6502.Core;

/// <summary>
/// Hardware constants for the 6502 CPU emulator.
/// Centralizes all magic numbers (addresses, page sizes, bit masks) for maintainability.
/// </summary>
public static class CpuConstants
{
    // ── Stack Management ──────────────────────────────────────────────────────
    /// <summary>Base address of the 6502 stack page (0x0100–0x01FF).</summary>
    public const ushort STACK_PAGE_BASE = 0x0100;
    
    /// <summary>Stack page size (256 bytes).</summary>
    public const ushort STACK_PAGE_SIZE = 0x0100;
    
    /// <summary>Initial stack pointer value after reset (0xFD).</summary>
    /// <remarks>
    /// The 6502 starts with SP=0xFD because three bytes are pushed during reset:
    /// 1. PC high byte
    /// 2. PC low byte
    /// 3. Status flags byte
    /// Stack grows downward from 0x01FF, so after reset: SP=0xFD, meaning next push goes to 0x01FC.
    /// </remarks>
    public const byte INITIAL_STACK_POINTER = 0xFD;

    // ── Interrupt Vectors ─────────────────────────────────────────────────────
    /// <summary>Reset vector address (0xFFFC–0xFFFD). Points to program entry point after power-on or reset.</summary>
    public const ushort RESET_VECTOR = 0xFFFC;
    
    /// <summary>Non-maskable interrupt (NMI) vector address (0xFFFA–0xFFFB). Always serviced regardless of I flag.</summary>
    public const ushort NMI_VECTOR = 0xFFFA;
    
    /// <summary>Maskable interrupt (IRQ) and BRK vector address (0xFFFE–0xFFFF). Serviced only if I flag is clear.</summary>
    public const ushort IRQ_VECTOR = 0xFFFE;

    // ── Address/Page Masks ────────────────────────────────────────────────────
    /// <summary>Page boundary mask (0xFF00). Used to detect page crossings.</summary>
    /// <remarks>Page cross detection: (addr1 & PAGE_MASK) != (addr2 & PAGE_MASK) means addr1 and addr2 are on different pages.</remarks>
    public const ushort PAGE_MASK = 0xFF00;
    
    /// <summary>Page size in bytes (256).</summary>
    public const ushort PAGE_SIZE = 0x0100;

    // ── Bit Masks (Individual Bits) ───────────────────────────────────────────
    /// <summary>Bit 0 mask (0x01). Used for LSB extraction and Carry bit operations.</summary>
    public const byte BIT_0_MASK = 0x01;
    
    /// <summary>Bit 1 mask (0x02).</summary>
    public const byte BIT_1_MASK = 0x02;
    
    /// <summary>Bit 2 mask (0x04).</summary>
    public const byte BIT_2_MASK = 0x04;
    
    /// <summary>Bit 3 mask (0x08).</summary>
    public const byte BIT_3_MASK = 0x08;
    
    /// <summary>Bit 4 mask (0x10).</summary>
    public const byte BIT_4_MASK = 0x10;
    
    /// <summary>Bit 5 mask (0x20).</summary>
    public const byte BIT_5_MASK = 0x20;
    
    /// <summary>Bit 6 mask (0x40). Used for Overflow flag operations.</summary>
    public const byte BIT_6_MASK = 0x40;
    
    /// <summary>Bit 7 mask (0x80). Used for sign bit (MSB) and Negative flag operations.</summary>
    public const byte BIT_7_MASK = 0x80;

    // ── Processor Status Flag Bit Positions ───────────────────────────────────
    /// <summary>Bit position of Carry flag (0).</summary>
    public const byte C_FLAG_BIT = 0;
    
    /// <summary>Bit position of Zero flag (1).</summary>
    public const byte Z_FLAG_BIT = 1;
    
    /// <summary>Bit position of Interrupt Disable flag (2).</summary>
    public const byte I_FLAG_BIT = 2;
    
    /// <summary>Bit position of Decimal mode flag (3).</summary>
    public const byte D_FLAG_BIT = 3;
    
    /// <summary>Bit position of Break flag (4). Set when BRK instruction executed.</summary>
    public const byte B_FLAG_BIT = 4;
    
    /// <summary>Bit position of Overflow flag (6).</summary>
    public const byte V_FLAG_BIT = 6;
    
    /// <summary>Bit position of Negative flag (7).</summary>
    public const byte N_FLAG_BIT = 7;
}
