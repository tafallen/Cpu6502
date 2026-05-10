using Cpu6502.Core;

namespace Machines.Electron;

/// <summary>
/// Acorn Electron ULA (Uncommitted Logic Array) — unified I/O hub.
/// 
/// Address map ($8000–$FFFF):
///   $8000–$BFFF  Paged ROM (16 KB, selected by page register at $FE05[3:0])
///   $C000–$FBFF  OS ROM (lower 16 KB minus top 1 KB)
///   $FC00–$FEFF  ULA MMIO registers (partial decode: only low 2 bits significant)
///   $FF00–$FFFF  OS ROM (top 256 bytes, interrupt vectors)
/// 
/// MMIO registers (partial decode using address bits [1:0]):
///   Register 0 (addresses $FE00, $FE04, $FE08, $FE0C, ...): 
///     Read:  Interrupt Status register
///     Write: Interrupt Clear register (write-to-clear semantics)
///   Register 1 (addresses $FE01, $FE05, $FE09, $FE0D, ...):
///     Read:  Reserved/undefined (cassette shift in hardware)
///     Write: ROM page select (low nibble) + interrupt enable mask (high nibble)
///   Register 2 (addresses $FE02, $FE06, $FE0A, $FE0E, ...):
///     Reserved/undefined
///   Register 3 (addresses $FE03, $FE07, $FE0B, $FE0F, ...):
///     Read:  Keyboard row state for latched column
///     Write: Cassette motor + tone control register
/// 
/// Interrupt Flags (bit positions in Status and Enable registers):
///   Bit 7: Master interrupt (read-only, OR of all enabled pending interrupts)
///   Bit 6: Power-on reset flag
///   Bit 5: /RDY — cassette input edge
///   Bit 4: /RTC — 100 Hz real-time clock tick
///   Bit 3: Display end (end of active display, used as VBL)
///   Bit 2: /DISP — display start (fires at top of frame)
///   Bit 1: Cassette transmit empty
///   Bit 0: Cassette receive full
/// </summary>
public sealed class ElectronUla : IBus
{
    // ── Interrupt state ──────────────────────────────────────────────────────
    private byte _interruptStatus;   // Pending interrupt flags (bits [6:0])
    private byte _interruptEnable;   // Interrupt enable mask (bits [7:4], written to $FE05 high nibble)

    // ── Control registers ────────────────────────────────────────────────────
    private byte _romPage;           // ROM page selector (bits [3:0] of $FE05)
    private byte _keyboardColumn;    // Currently latched column for keyboard matrix
    private byte _cassetteControl;   // Motor, tone divisor, transmit bit ($FE07 write)

    /// <summary>Constructor. ROMs will be added in Phase 2.</summary>
    public ElectronUla()
    {
        _interruptStatus = 0x00;
        _interruptEnable = 0x00;
        _romPage = 0;
        _keyboardColumn = 0;
        _cassetteControl = 0x00;
    }

    /// <summary>Read from ULA address space ($8000–$FFFF).</summary>
    public byte Read(ushort address)
    {
        // Phase 1: Handle MMIO registers only. ROM reads will be added in Phase 2.
        if (address >= 0xFC00 && address <= 0xFEFF)
        {
            int registerOffset = GetRegisterOffset(address);
            return ReadRegister(registerOffset);
        }

        // Placeholder for ROM reads (Phase 2)
        return 0xFF;  // Open bus
    }

    /// <summary>Write to ULA address space ($8000–$FFFF).</summary>
    public void Write(ushort address, byte value)
    {
        // Phase 1: Handle MMIO registers only.
        if (address >= 0xFC00 && address <= 0xFEFF)
        {
            int registerOffset = GetRegisterOffset(address);
            WriteRegister(registerOffset, value);
        }

        // Placeholder for ROM writes (will be ignored in Phase 2)
    }

    /// <summary>Decode register number from address using partial decode (low 3 bits).</summary>
    private int GetRegisterOffset(ushort address)
    {
        // Only address bits [2:0] are significant within ULA range.
        // $FE00, $FE08, $FE10, ... map to register 0 (ISR/Clear)
        // $FE04, $FE0C, $FE14, ... map to register 4 (Cassette shift)
        // $FE05, $FE0D, $FE15, ... map to register 5 (ROM page/enable)
        // $FE07, $FE0F, $FE17, ... map to register 7 (Keyboard/cassette control)
        return address & 0x07;
    }

    /// <summary>Read a ULA register (only specific offsets 0, 4, 5, 7 respond).</summary>
    private byte ReadRegister(int offset)
    {
        return offset switch
        {
            0 => ReadInterruptStatus(),
            4 => 0x00,  // Cassette shift register (not implemented in Phase 1)
            5 => 0x00,  // ROM page/enable read as 0 (write-only in hardware)
            7 => 0x0F,  // Keyboard (Phase 1: return open bus)
            _ => 0xFF   // Other offsets: open bus
        };
    }

    /// <summary>Write a ULA register (only specific offsets 0, 4, 5, 7 respond).</summary>
    private void WriteRegister(int offset, byte value)
    {
        switch (offset)
        {
            case 0:
                WriteInterruptClear(value);
                break;
            case 4:
                // Cassette shift register (not implemented in Phase 1)
                break;
            case 5:
                // ROM page + interrupt enable
                // ($FE05 write: high nibble = enable, low nibble = page)
                SetRomPageAndInterruptEnable(value);
                break;
            case 7:
                WriteCassetteControl(value);
                break;
        }
    }

    /// <summary>Interrupt Status Register (read at $FE00, $FE08, etc).</summary>
    private byte ReadInterruptStatus()
    {
        // Bit 7 is master interrupt: set if any enabled interrupt is pending
        byte masterBit = ((_interruptStatus & _interruptEnable) & 0x7F) != 0 ? (byte)0x80 : (byte)0x00;
        return (byte)(masterBit | _interruptStatus);
    }

    /// <summary>Interrupt Clear (write to $FE00) — write-to-clear semantics.</summary>
    private void WriteInterruptClear(byte value)
    {
        // Writing a 1 to a bit clears that interrupt flag
        _interruptStatus &= (byte)~value;
    }

    /// <summary>ROM Page and Interrupt Enable (read/write at $FE05).</summary>
    public void SetRomPageAndInterruptEnable(byte value)
    {
        _romPage = (byte)(value & 0x0F);           // Low nibble: ROM page (0–15)
        _interruptEnable = (byte)(value & 0xF0);  // High nibble: interrupt enable mask
    }

    /// <summary>Query current ROM page.</summary>
    public byte RomPage => _romPage;

    /// <summary>Query interrupt enable mask.</summary>
    public byte InterruptEnableMask => _interruptEnable;

    /// <summary>Keyboard Row (read at $FE07) — returns row state for latched column.</summary>
    private byte ReadKeyboardRow()
    {
        // Phase 1: Return dummy value (0x00 = all keys pressed, or 0x0F = no keys).
        // Phase 2: Will query ElectronKeyboardAdapter with latched column.
        return 0x0F;  // Open bus (no keys)
    }

    /// <summary>Cassette Control (write to $FE07).</summary>
    private void WriteCassetteControl(byte value)
    {
        _cassetteControl = value;
        // Phase 2: Parse motor bit, tone divisor, transmit bit
    }

    /// <summary>Query cassette control state.</summary>
    public byte CassetteControl => _cassetteControl;

    /// <summary>Set keyboard column (latched from address bus during read of $FE0x).</summary>
    public void LatchKeyboardColumn(byte column)
    {
        _keyboardColumn = column;
    }

    /// <summary>Query latched keyboard column.</summary>
    public byte KeyboardColumn => _keyboardColumn;

    /// <summary>Set an interrupt pending (for use by timer and display interrupt sources).</summary>
    public void SetInterruptPending(byte interruptBit)
    {
        _interruptStatus |= interruptBit;
    }

    /// <summary>Clear an interrupt pending.</summary>
    public void ClearInterruptPending(byte interruptBit)
    {
        _interruptStatus &= (byte)~interruptBit;
    }

    /// <summary>Query current interrupt status.</summary>
    public byte InterruptStatus => _interruptStatus;
}
