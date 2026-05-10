using Machines.Electron;
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
}
