using System;
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

    // ── ROM storage ──────────────────────────────────────────────────────────
    private readonly byte[] _pagedRomBank0;      // Pages 0–3 (external cartridge 1)
    private readonly byte[] _pagedRomBank1;      // Pages 4–7 (external cartridge 2)
    private readonly byte[] _pagedRomBank2;      // Pages 8–9 (keyboard handler + OS extension)
    private readonly byte[] _pagedRomBank3;      // Pages 10–11 (BBC BASIC II)
    private readonly byte[] _osRom;              // Pages 12–15 (OS ROM, 16 KB)

    /// <summary>
    /// Constructor accepting built-in ROM images.
    /// 
    /// ROM layout:
    ///   Pages 0–3:   External cartridge 1 (not populated → returns 0xFF)
    ///   Pages 4–7:   External cartridge 2 (not populated → returns 0xFF)
    ///   Pages 8–9:   Keyboard handler + OS extension (built-in, 16 KB total for 2 pages)
    ///   Pages 10–11: BBC BASIC II (built-in, 16 KB total for 2 pages)
    ///   Pages 12–15: OS ROM (16 KB, always visible at $C000–$FBFF and $FF00–$FFFF)
    /// 
    /// On an unexpanded machine, pass null for external cartridges (they will return open bus).
    /// </summary>
    public ElectronUla(byte[]? basicRom = null, byte[]? osRom = null, byte[]? keyboardHandlerRom = null, byte[]? osExtensionRom = null)
    {
        _interruptStatus = 0x00;
        _interruptEnable = 0x00;
        _romPage = 0;
        _keyboardColumn = 0;
        _cassetteControl = 0x00;

        // Initialize ROM banks
        // Pages 0–3: External cartridge 1 (not populated)
        _pagedRomBank0 = new byte[0x4000];  // 16 KB, initialized to 0 (will return as 0xFF in reads)

        // Pages 4–7: External cartridge 2 (not populated)
        _pagedRomBank1 = new byte[0x4000];  // 16 KB, initialized to 0

        // Pages 8–9: Keyboard handler (page 8, 8 KB) + OS extension (page 9, 8 KB)
        _pagedRomBank2 = new byte[0x4000];  // 16 KB total
        if (keyboardHandlerRom != null)
            Array.Copy(keyboardHandlerRom, 0, _pagedRomBank2, 0, Math.Min(keyboardHandlerRom.Length, 0x2000));
        if (osExtensionRom != null)
            Array.Copy(osExtensionRom, 0, _pagedRomBank2, 0x2000, Math.Min(osExtensionRom.Length, 0x2000));

        // Pages 10–11: BBC BASIC II (16 KB total)
        _pagedRomBank3 = new byte[0x4000];  // 16 KB
        if (basicRom != null)
            Array.Copy(basicRom, 0, _pagedRomBank3, 0, Math.Min(basicRom.Length, 0x4000));

        // Pages 12–15: OS ROM (16 KB)
        _osRom = new byte[0x4000];  // 16 KB
        if (osRom != null)
            Array.Copy(osRom, 0, _osRom, 0, Math.Min(osRom.Length, 0x4000));
    }

    /// <summary>Read from ULA address space ($8000–$FFFF).</summary>
    public byte Read(ushort address)
    {
        // MMIO registers at $FC00–$FEFF
        if (address >= 0xFC00 && address <= 0xFEFF)
        {
            int registerOffset = GetRegisterOffset(address);
            return ReadRegister(registerOffset);
        }

        // Paged ROM at $8000–$BFFF (16 KB window selected by page register)
        if (address >= 0x8000 && address <= 0xBFFF)
        {
            return ReadPagedRom(address);
        }

        // OS ROM at $C000–$FBFF (lower 16 KB minus top 1 KB)
        if (address >= 0xC000 && address <= 0xFBFF)
        {
            ushort offset = (ushort)(address - 0xC000);
            return _osRom[offset];
        }

        // OS ROM at $FF00–$FFFF (top 256 bytes, interrupt vectors)
        if (address >= 0xFF00 && address <= 0xFFFF)
        {
            ushort offset = (ushort)(address - 0xC000);  // $FF00 = offset $3F00 in OS ROM
            return _osRom[offset];
        }

        // Unmapped
        return 0xFF;
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

    /// <summary>Read from paged ROM window ($8000–$BFFF) based on current page register.</summary>
    private byte ReadPagedRom(ushort address)
    {
        // Offset within the 16 KB paged window
        ushort offset = (ushort)(address - 0x8000);

        return _romPage switch
        {
            // Pages 0–3: External cartridge 1
            0 or 1 or 2 or 3 => ReadExternalCartridge1(offset),

            // Pages 4–7: External cartridge 2
            4 or 5 or 6 or 7 => ReadExternalCartridge2(offset),

            // Pages 8–9: Keyboard handler + OS extension
            8 or 9 => _pagedRomBank2[offset],

            // Pages 10–11: BBC BASIC II
            10 or 11 => _pagedRomBank3[offset],

            // Pages 12–15: Not used (open bus)
            _ => 0xFF
        };
    }

    /// <summary>Read from external cartridge 1 (pages 0–3) — returns open bus if not populated.</summary>
    private byte ReadExternalCartridge1(ushort offset)
    {
        // Offset determines which page (0–3) within the cartridge
        // For base machine (unexpanded), no cartridge is present, so return open bus
        // If populated, would select the appropriate page and return byte
        return 0xFF;  // Open bus (not populated)
    }

    /// <summary>Read from external cartridge 2 (pages 4–7) — returns open bus if not populated.</summary>
    private byte ReadExternalCartridge2(ushort offset)
    {
        // Similar to cartridge 1, but for pages 4–7
        return 0xFF;  // Open bus (not populated)
    }
}
