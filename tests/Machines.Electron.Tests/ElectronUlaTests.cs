using Machines.Electron;
using System;
using Xunit;

namespace Machines.Electron.Tests;

/// <summary>Unit tests for ElectronUla MMIO register support.</summary>
public class ElectronUlaTests
{
    [Fact]
    public void InterruptStatusRegister_InitiallyReturnsZero()
    {
        var ula = new ElectronUla();
        byte status = ula.Read(0xFE00);
        Assert.Equal(0x00, status);
    }

    [Fact]
    public void InterruptStatusRegister_MasterBitClear_WhenNoInterruptsPending()
    {
        var ula = new ElectronUla();
        byte status = ula.Read(0xFE00);
        Assert.Equal(0x00, status & 0x80);  // Bit 7 clear
    }

    [Fact]
    public void InterruptStatusRegister_MasterBitSet_WhenInterruptPendingAndEnabled()
    {
        var ula = new ElectronUla();
        ula.SetInterruptPending(0x10);  // Bit 4 (RTC — maskable)
        ula.SetRomPageAndInterruptEnable(0x10);  // Enable mask bits [7:4]: bit 4 enabled
        byte status = ula.Read(0xFE00);
        Assert.Equal(0x80, status & 0x80);  // Master bit set because bit 4 is enabled
    }

    [Fact]
    public void InterruptStatusRegister_MasterBitClear_WhenInterruptPendingButDisabled()
    {
        var ula = new ElectronUla();
        ula.SetInterruptPending(0x08);  // Bit 3 pending
        ula.SetRomPageAndInterruptEnable(0x00);  // Not enabled
        byte status = ula.Read(0xFE00);
        Assert.Equal(0x00, status & 0x80);  // Master bit clear
    }

    [Fact]
    public void InterruptStatusRegister_ReportsPendingBits()
    {
        var ula = new ElectronUla();
        ula.SetInterruptPending(0x18);  // Bits 3 and 4
        byte status = ula.Read(0xFE00);
        Assert.Equal(0x18, status & 0x7F);  // Bits 3 and 4 set, bit 7 ignored
    }

    [Fact]
    public void InterruptClear_WritesToFE00_ClearsPendingBits()
    {
        var ula = new ElectronUla();
        ula.SetInterruptPending(0x1F);  // Bits 0–4 pending
        ula.Write(0xFE00, 0x08);  // Clear bit 3
        byte status = ula.Read(0xFE00);
        Assert.Equal(0x17, status & 0x7F);  // Bits 0, 1, 2, 4 remain
    }

    [Fact]
    public void InterruptClear_ClearsOnlyWrittenBits()
    {
        var ula = new ElectronUla();
        ula.SetInterruptPending(0x7F);  // All bits 0–6 pending
        ula.Write(0xFE00, 0x05);  // Clear bits 0 and 2 only
        byte status = ula.Read(0xFE00);
        Assert.Equal(0x7A, status & 0x7F);  // Bits 1, 3, 4, 5, 6 remain
    }

    [Fact]
    public void InterruptClear_WritingZero_NoEffect()
    {
        var ula = new ElectronUla();
        ula.SetInterruptPending(0x3F);  // All bits 0–5 pending
        ula.Write(0xFE00, 0x00);  // Write 0
        byte status = ula.Read(0xFE00);
        Assert.Equal(0x3F, status & 0x7F);  // All pending bits still set
    }

    [Fact]
    public void RomPageRegister_StoresPageValue()
    {
        var ula = new ElectronUla();
        ula.SetRomPageAndInterruptEnable(0x05);  // Page 5
        Assert.Equal(0x05, ula.RomPage);
    }

    [Fact]
    public void RomPageRegister_MasksTo4Bits()
    {
        var ula = new ElectronUla();
        ula.SetRomPageAndInterruptEnable(0x0F);  // Page 15 (max valid)
        Assert.Equal(0x0F, ula.RomPage);
    }

    [Fact]
    public void InterruptEnableMask_StoresHighNibble()
    {
        var ula = new ElectronUla();
        ula.SetRomPageAndInterruptEnable(0xF0);  // Mask $F0
        Assert.Equal(0xF0, ula.InterruptEnableMask);
    }

    [Fact]
    public void RomPageAndEnableMask_BothStoredFromSameWrite()
    {
        var ula = new ElectronUla();
        ula.SetRomPageAndInterruptEnable(0xA7);  // $A7: page 7, enable $A0
        Assert.Equal(0x07, ula.RomPage);
        Assert.Equal(0xA0, ula.InterruptEnableMask);
    }

    [Fact]
    public void PartialDecode_FE00_FE08_FE10_HitSameRegister()
    {
        var ula = new ElectronUla();
        ula.SetInterruptPending(0x04);  // Set bit 2
        byte r1 = ula.Read(0xFE00);
        byte r2 = ula.Read(0xFE08);
        byte r3 = ula.Read(0xFE10);
        Assert.Equal(r1, r2);
        Assert.Equal(r2, r3);
    }

    [Fact]
    public void PartialDecode_FE04_FE0C_FE14_HitSameRegister()
    {
        var ula = new ElectronUla();
        ula.Write(0xFE04, 0x55);  // Write to cassette shift register (not stored in Phase 1)
        ula.Write(0xFE0C, 0xAA);  // Write again (same register, bits [2:0] = 100)
        // Phase 1: no observable effect, but both should hit the same register
        byte r1 = ula.Read(0xFE04);  // Should return 0x00
        byte r2 = ula.Read(0xFE0C);  // Should return 0x00
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void PartialDecode_FE05_FE0D_FE15_HitSameRegister()
    {
        var ula = new ElectronUla();
        ula.Write(0xFE05, 0xA3);  // Set page 3, enable mask $A0
        ula.Write(0xFE0D, 0x5C);  // Overwrite (same register, bits [2:0] = 101)
        Assert.Equal(0x0C, ula.RomPage);  // Page updated to 0x0C
        Assert.Equal(0x50, ula.InterruptEnableMask);  // Mask updated to 0x50
    }

    [Fact]
    public void PartialDecode_FE07_FE0F_FE17_HitSameRegister()
    {
        var ula = new ElectronUla();
        ula.Write(0xFE07, 0x81);  // Motor on, bit 0 set
        ula.Write(0xFE0F, 0x42);  // Overwrite (same register, bits [2:0] = 111)
        Assert.Equal(0x42, ula.CassetteControl);  // Last write wins
        ula.Write(0xFE17, 0xFF);  // Write again (same register)
        Assert.Equal(0xFF, ula.CassetteControl);
    }

    [Fact]
    public void CassetteControl_StoresWriteValue()
    {
        var ula = new ElectronUla();
        ula.Write(0xFE07, 0x81);  // Motor bit set, data bit set
        Assert.Equal(0x81, ula.CassetteControl);
    }

    [Fact]
    public void KeyboardColumn_LatchesColumnValue()
    {
        var ula = new ElectronUla();
        ula.LatchKeyboardColumn(5);
        Assert.Equal(5, ula.KeyboardColumn);
    }

    [Fact]
    public void KeyboardColumn_CanUpdateLatched()
    {
        var ula = new ElectronUla();
        ula.LatchKeyboardColumn(0);
        Assert.Equal(0, ula.KeyboardColumn);
        ula.LatchKeyboardColumn(13);
        Assert.Equal(13, ula.KeyboardColumn);
    }

    [Fact]
    public void KeyboardRead_ReturnsRow()
    {
        var ula = new ElectronUla();
        byte row = ula.Read(0xFE07);
        Assert.Equal(0x0F, row);  // Phase 1: dummy value (open bus)
    }

    [Fact]
    public void RomRead_ReturnsOpenBus()
    {
        var ula = new ElectronUla();
        byte value = ula.Read(0x8000);  // Paged ROM range
        Assert.Equal(0xFF, value);  // Phase 1: open bus
    }

    [Fact]
    public void RomWrite_HasNoEffect()
    {
        var ula = new ElectronUla();
        ula.Write(0x8000, 0xA9);  // Try to write to paged ROM
        byte value = ula.Read(0x8000);
        Assert.Equal(0xFF, value);  // No change
    }

    [Fact]
    public void MultipleInterruptsCanBePending()
    {
        var ula = new ElectronUla();
        ula.SetInterruptPending(0x04);  // Display start
        ula.SetInterruptPending(0x08);  // Display end
        ula.SetInterruptPending(0x10);  // RTC
        byte status = ula.Read(0xFE00);
        Assert.Equal(0x1C, status & 0x7F);
    }

    [Fact]
    public void InterruptClearing_PreservesOtherBits()
    {
        var ula = new ElectronUla();
        ula.SetInterruptPending(0x1F);  // Bits 0–4
        ula.Write(0xFE00, 0x0A);  // Clear bits 1 and 3
        byte status = ula.Read(0xFE00);
        Assert.Equal(0x15, status & 0x7F);  // Bits 0, 2, 4 remain
    }

    [Fact]
    public void ClearInterruptPending_RemovesBit()
    {
        var ula = new ElectronUla();
        ula.SetInterruptPending(0x1F);
        ula.ClearInterruptPending(0x08);  // Clear bit 3
        Assert.Equal(0x17, ula.InterruptStatus);
    }

    [Fact]
    public void ClearInterruptPending_MultipleBits()
    {
        var ula = new ElectronUla();
        ula.SetInterruptPending(0x7F);  // All bits 0–6
        ula.ClearInterruptPending(0x55);  // Clear bits 0, 2, 4, 6
        Assert.Equal(0x2A, ula.InterruptStatus);  // Bits 1, 3, 5 remain
    }

    [Fact]
    public void InterruptStatus_IsIndependentFromEnable()
    {
        var ula = new ElectronUla();
        ula.SetRomPageAndInterruptEnable(0xF0);  // Enable all
        ula.SetInterruptPending(0x00);  // No interrupts
        byte status = ula.Read(0xFE00);
        Assert.Equal(0x00, status & 0x7F);  // Bit 7 clear (no pending)
    }

    [Fact]
    public void Register1_Reads_OpenBus()
    {
        var ula = new ElectronUla();
        byte value = ula.Read(0xFE01);  // Offset 1 (undefined)
        Assert.Equal(0xFF, value);  // Open bus
    }

    [Fact]
    public void Register2_Reads_OpenBus()
    {
        var ula = new ElectronUla();
        byte value = ula.Read(0xFE02);  // Offset 2 (undefined)
        Assert.Equal(0xFF, value);  // Open bus
    }

    [Fact]
    public void UnmappedWrite_ToRegister1_HasNoEffect()
    {
        var ula = new ElectronUla();
        ula.SetRomPageAndInterruptEnable(0x05);
        ula.Write(0xFE01, 0xFF);  // Try to write to reg 1
        Assert.Equal(0x05, ula.RomPage);  // No change
    }

    // ── ROM Paging Tests ─────────────────────────────────────────────────────

    [Fact]
    public void RomPaging_BasicRomBoot()
    {
        // Prepare BASIC ROM (pages 10–11) with known pattern
        byte[] basicRom = new byte[0x4000];
        for (int i = 0; i < basicRom.Length; i++)
            basicRom[i] = (byte)(i & 0xFF);

        var ula = new ElectronUla(basicRom: basicRom);

        // Page 10 or 11 (BASIC) should return ROM data
        ula.SetRomPageAndInterruptEnable(0x0A);  // Page 10 (lower BASIC)
        byte value = ula.Read(0x8000);
        Assert.Equal(0x00, value);  // First byte of BASIC

        // Page 11 should also work (uses same ROM bank in this impl)
        ula.SetRomPageAndInterruptEnable(0x0B);  // Page 11 (upper BASIC)
        value = ula.Read(0x8000);
        Assert.Equal(0x00, value);
    }

    [Fact]
    public void RomPaging_KeyboardHandlerRom()
    {
        byte[] keyboardRom = new byte[0x2000];
        for (int i = 0; i < keyboardRom.Length; i++)
            keyboardRom[i] = (byte)(i & 0xFF);

        var ula = new ElectronUla(keyboardHandlerRom: keyboardRom);

        ula.SetRomPageAndInterruptEnable(0x08);  // Page 8 (keyboard handler)
        byte value = ula.Read(0x8000);
        Assert.Equal(0x00, value);  // First byte
    }

    [Fact]
    public void RomPaging_OsRom()
    {
        byte[] osRom = new byte[0x4000];
        for (int i = 0; i < osRom.Length; i++)
            osRom[i] = (byte)((i >> 8) & 0xFF);  // High byte of address

        var ula = new ElectronUla(osRom: osRom);

        // OS ROM should be visible at $C000–$FBFF regardless of page selection
        byte value = ula.Read(0xC000);
        Assert.Equal(0x00, value);

        value = ula.Read(0xD000);
        Assert.Equal(0x10, value);

        value = ula.Read(0xFBFF);
        Assert.Equal(0x3B, value);  // Offset 0x3BFF, high byte = 0x3B
    }

    [Fact]
    public void RomPaging_InterruptVectors()
    {
        byte[] osRom = new byte[0x4000];
        osRom[0x3FFA] = 0x11;  // NMI vector (offset $FFFA → offset $3FFA in 16 KB OS ROM)
        osRom[0x3FFB] = 0x22;
        osRom[0x3FFC] = 0x33;  // RESET vector
        osRom[0x3FFD] = 0x44;
        osRom[0x3FFE] = 0x55;  // IRQ vector
        osRom[0x3FFF] = 0x66;

        var ula = new ElectronUla(osRom: osRom);

        // Read interrupt vectors from $FFFA–$FFFF
        Assert.Equal(0x11, ula.Read(0xFFFA));
        Assert.Equal(0x22, ula.Read(0xFFFB));
        Assert.Equal(0x33, ula.Read(0xFFFC));
        Assert.Equal(0x44, ula.Read(0xFFFD));
        Assert.Equal(0x55, ula.Read(0xFFFE));
        Assert.Equal(0x66, ula.Read(0xFFFF));
    }

    [Fact]
    public void RomPaging_ExternalCartridge_ReturnsOpenBus()
    {
        var ula = new ElectronUla();

        // Pages 0–3 (external cartridge 1) should return open bus
        ula.SetRomPageAndInterruptEnable(0x00);
        Assert.Equal(0xFF, ula.Read(0x8000));

        ula.SetRomPageAndInterruptEnable(0x03);
        Assert.Equal(0xFF, ula.Read(0x8000));

        // Pages 4–7 (external cartridge 2) should return open bus
        ula.SetRomPageAndInterruptEnable(0x04);
        Assert.Equal(0xFF, ula.Read(0x8000));

        ula.SetRomPageAndInterruptEnable(0x07);
        Assert.Equal(0xFF, ula.Read(0x8000));
    }

    [Fact]
    public void RomPaging_UnusedPages_ReturnsOpenBus()
    {
        var ula = new ElectronUla();

        // Pages 12–15 not used (should return open bus)
        for (byte page = 12; page <= 15; page++)
        {
            ula.SetRomPageAndInterruptEnable(page);
            Assert.Equal(0xFF, ula.Read(0x8000));
        }
    }

    [Fact]
    public void RomPaging_PagedRomBoundaryReads()
    {
        byte[] basicRom = new byte[0x4000];
        basicRom[0x0000] = 0xAA;  // Start
        basicRom[0x3FFF] = 0xBB;  // End

        var ula = new ElectronUla(basicRom: basicRom);
        ula.SetRomPageAndInterruptEnable(0x0A);  // Page 10

        Assert.Equal(0xAA, ula.Read(0x8000));  // Start of paged window
        Assert.Equal(0xBB, ula.Read(0xBFFF));  // End of paged window
    }

    [Fact]
    public void RomPaging_PageSwitchingWorks()
    {
        byte[] basicRom = new byte[0x4000];
        for (int i = 0; i < basicRom.Length; i++)
            basicRom[i] = (byte)(i & 0xFF);

        byte[] keyboardRom = new byte[0x2000];
        for (int i = 0; i < keyboardRom.Length; i++)
            keyboardRom[i] = 0xCC;

        var ula = new ElectronUla(basicRom: basicRom, keyboardHandlerRom: keyboardRom);

        // Read from BASIC page
        ula.SetRomPageAndInterruptEnable(0x0A);
        byte value1 = ula.Read(0x8000);

        // Switch to keyboard handler page
        ula.SetRomPageAndInterruptEnable(0x08);
        byte value2 = ula.Read(0x8000);

        // Values should differ
        Assert.NotEqual(value1, value2);
        Assert.Equal(0xCC, value2);
    }

    [Fact]
    public void RomPaging_OsRomAndPagedRomDoNotConflict()
    {
        byte[] basicRom = new byte[0x4000];
        basicRom[0] = 0x11;

        byte[] osRom = new byte[0x4000];
        osRom[0] = 0x22;  // Different from BASIC

        var ula = new ElectronUla(basicRom: basicRom, osRom: osRom);
        ula.SetRomPageAndInterruptEnable(0x0A);  // Page 10 (BASIC)

        // Paged ROM read should return BASIC byte
        Assert.Equal(0x11, ula.Read(0x8000));

        // OS ROM read should return OS byte
        Assert.Equal(0x22, ula.Read(0xC000));
    }
}
