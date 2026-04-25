using Xunit;

namespace Cpu6502.Tests;

/// <summary>
/// Exercises the addressing mode variants that aren't covered by the main
/// per-instruction test files. Keeps each test minimal — just enough to confirm
/// the addressing mode routes to the right memory location and records the
/// correct cycle count.
/// </summary>
public class AddressingModeVariantTests : CpuFixture
{
    // ── LDX addressing modes ──────────────────────────────────────────────────

    [Fact]
    public void LDX_ZeroPageY()
    {
        Ram.Write(0x0005, 0x77);
        Load(0x0200, 0xA0, 0x02, 0xB6, 0x03); // LDY #$02, LDX $03,Y → $05
        Step(2);
        Assert.Equal(0x77, Cpu.X);
    }

    [Fact]
    public void LDX_ZeroPageY_Costs4Cycles()
    {
        Load(0x0200, 0xA0, 0x00, 0xB6, 0x00);
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(4UL, Cpu.TotalCycles - after);
    }

    [Fact]
    public void LDX_AbsoluteY()
    {
        Ram.Write(0x0302, 0x88);
        Load(0x0200, 0xA0, 0x02, 0xBE, 0x00, 0x03); // LDY #$02, LDX $0300,Y → $0302
        Step(2);
        Assert.Equal(0x88, Cpu.X);
    }

    [Fact]
    public void LDX_AbsoluteY_PageCross_Costs5Cycles()
    {
        Ram.Write(0x0100, 0x55);
        Load(0x0200, 0xA0, 0x01, 0xBE, 0xFF, 0x00); // LDY #$01, LDX $00FF,Y → $0100
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(5UL, Cpu.TotalCycles - after);
    }

    // ── LDY addressing modes ──────────────────────────────────────────────────

    [Fact]
    public void LDY_ZeroPageX()
    {
        Ram.Write(0x0006, 0x44);
        Load(0x0200, 0xA2, 0x02, 0xB4, 0x04); // LDX #$02, LDY $04,X → $06
        Step(2);
        Assert.Equal(0x44, Cpu.Y);
    }

    [Fact]
    public void LDY_AbsoluteX()
    {
        Ram.Write(0x0402, 0x33);
        Load(0x0200, 0xA2, 0x02, 0xBC, 0x00, 0x04); // LDX #$02, LDY $0400,X → $0402
        Step(2);
        Assert.Equal(0x33, Cpu.Y);
    }

    [Fact]
    public void LDY_AbsoluteX_PageCross_Costs5Cycles()
    {
        Ram.Write(0x0100, 0x11);
        Load(0x0200, 0xA2, 0x01, 0xBC, 0xFF, 0x00); // LDX #$01, LDY $00FF,X
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(5UL, Cpu.TotalCycles - after);
    }

    // ── STA additional modes ──────────────────────────────────────────────────

    [Fact]
    public void STA_AbsoluteY_StoresAndCosts5Cycles()
    {
        Load(0x0200,
            0xA0, 0x02,             // LDY #$02
            0xA9, 0xAA,             // LDA #$AA
            0x99, 0x00, 0x04);      // STA $0400,Y → $0402
        Step(2);
        var after = CycleSnapshot();
        Step();
        Assert.Equal(5UL, Cpu.TotalCycles - after);
        Assert.Equal(0xAA, Ram.Read(0x0402));
    }

    [Fact]
    public void STA_IndirectX()
    {
        Ram.Write(0x0024, 0x00); Ram.Write(0x0025, 0x05); // ptr at $24/$25 → $0500
        Load(0x0200,
            0xA2, 0x04,             // LDX #$04
            0xA9, 0xBB,             // LDA #$BB
            0x81, 0x20);            // STA ($20,X) → ptr at $24 → $0500
        Step(3);
        Assert.Equal(0xBB, Ram.Read(0x0500));
    }

    [Fact]
    public void STA_IndirectY()
    {
        Ram.Write(0x0010, 0x00); Ram.Write(0x0011, 0x05); // ptr → $0500
        Load(0x0200,
            0xA0, 0x02,             // LDY #$02
            0xA9, 0xCC,             // LDA #$CC
            0x91, 0x10);            // STA ($10),Y → $0502
        Step(3);
        Assert.Equal(0xCC, Ram.Read(0x0502));
    }

    // ── STX / STY additional modes ────────────────────────────────────────────

    [Fact]
    public void STX_ZeroPageY()
    {
        Load(0x0200,
            0xA0, 0x01,             // LDY #$01
            0xA2, 0xDD,             // LDX #$DD
            0x96, 0x04);            // STX $04,Y → $05
        Step(3);
        Assert.Equal(0xDD, Ram.Read(0x0005));
    }

    [Fact]
    public void STY_ZeroPageX()
    {
        Load(0x0200,
            0xA2, 0x01,             // LDX #$01
            0xA0, 0xEE,             // LDY #$EE
            0x94, 0x04);            // STY $04,X → $05
        Step(3);
        Assert.Equal(0xEE, Ram.Read(0x0005));
    }

    // ── AND additional addressing modes ──────────────────────────────────────

    [Fact]
    public void AND_ZeroPageX()
    {
        Ram.Write(0x0005, 0x0F);
        Load(0x0200, 0xA2, 0x02, 0xA9, 0xFF, 0x35, 0x03); // LDX #2, LDA #$FF, AND $03,X
        Step(3);
        Assert.Equal(0x0F, Cpu.A);
    }

    [Fact]
    public void AND_AbsoluteX()
    {
        Ram.Write(0x0402, 0xF0);
        Load(0x0200, 0xA2, 0x02, 0xA9, 0xFF, 0x3D, 0x00, 0x04); // LDX #2, LDA #$FF, AND $0400,X
        Step(3);
        Assert.Equal(0xF0, Cpu.A);
    }

    [Fact]
    public void AND_AbsoluteY()
    {
        Ram.Write(0x0402, 0x0F);
        Load(0x0200, 0xA0, 0x02, 0xA9, 0xFF, 0x39, 0x00, 0x04); // LDY #2, LDA #$FF, AND $0400,Y
        Step(3);
        Assert.Equal(0x0F, Cpu.A);
    }

    [Fact]
    public void AND_IndexedIndirectX()
    {
        Ram.Write(0x0024, 0x00); Ram.Write(0x0025, 0x05);
        Ram.Write(0x0500, 0x33);
        Load(0x0200, 0xA2, 0x04, 0xA9, 0xFF, 0x21, 0x20); // LDX #4, LDA #$FF, AND ($20,X)
        Step(3);
        Assert.Equal(0x33, Cpu.A);
    }

    [Fact]
    public void AND_IndirectIndexedY()
    {
        Ram.Write(0x0010, 0x00); Ram.Write(0x0011, 0x05);
        Ram.Write(0x0502, 0x55);
        Load(0x0200, 0xA0, 0x02, 0xA9, 0xFF, 0x31, 0x10); // LDY #2, LDA #$FF, AND ($10),Y
        Step(3);
        Assert.Equal(0x55, Cpu.A);
    }

    // ── ORA / EOR additional modes ────────────────────────────────────────────

    [Fact]
    public void ORA_ZeroPageX()
    {
        Ram.Write(0x0005, 0xF0);
        Load(0x0200, 0xA2, 0x02, 0xA9, 0x0F, 0x15, 0x03); // LDX #2, LDA #$0F, ORA $03,X
        Step(3);
        Assert.Equal(0xFF, Cpu.A);
    }

    [Fact]
    public void ORA_AbsoluteX() { Ram.Write(0x0402, 0x80); Load(0x0200, 0xA2, 0x02, 0xA9, 0x01, 0x1D, 0x00, 0x04); Step(3); Assert.Equal(0x81, Cpu.A); }
    [Fact]
    public void ORA_AbsoluteY() { Ram.Write(0x0402, 0x70); Load(0x0200, 0xA0, 0x02, 0xA9, 0x01, 0x19, 0x00, 0x04); Step(3); Assert.Equal(0x71, Cpu.A); }
    [Fact]
    public void ORA_IndexedIndirectX() { Ram.Write(0x0024, 0x00); Ram.Write(0x0025, 0x05); Ram.Write(0x0500, 0xAB); Load(0x0200, 0xA2, 0x04, 0xA9, 0x00, 0x01, 0x20); Step(3); Assert.Equal(0xAB, Cpu.A); }
    [Fact]
    public void ORA_IndirectIndexedY() { Ram.Write(0x0010, 0x00); Ram.Write(0x0011, 0x05); Ram.Write(0x0503, 0xCD); Load(0x0200, 0xA0, 0x03, 0xA9, 0x00, 0x11, 0x10); Step(3); Assert.Equal(0xCD, Cpu.A); }

    [Fact]
    public void EOR_ZeroPageX() { Ram.Write(0x0005, 0xFF); Load(0x0200, 0xA2, 0x02, 0xA9, 0xFF, 0x55, 0x03); Step(3); Assert.Equal(0x00, Cpu.A); Assert.True(Cpu.Z); }
    [Fact]
    public void EOR_AbsoluteX() { Ram.Write(0x0402, 0xAA); Load(0x0200, 0xA2, 0x02, 0xA9, 0x55, 0x5D, 0x00, 0x04); Step(3); Assert.Equal(0xFF, Cpu.A); }
    [Fact]
    public void EOR_AbsoluteY() { Ram.Write(0x0402, 0xAA); Load(0x0200, 0xA0, 0x02, 0xA9, 0xFF, 0x59, 0x00, 0x04); Step(3); Assert.Equal(0x55, Cpu.A); }
    [Fact]
    public void EOR_IndexedIndirectX() { Ram.Write(0x0024, 0x00); Ram.Write(0x0025, 0x05); Ram.Write(0x0500, 0xF0); Load(0x0200, 0xA2, 0x04, 0xA9, 0x0F, 0x41, 0x20); Step(3); Assert.Equal(0xFF, Cpu.A); }
    [Fact]
    public void EOR_IndirectIndexedY() { Ram.Write(0x0010, 0x00); Ram.Write(0x0011, 0x05); Ram.Write(0x0501, 0xAA); Load(0x0200, 0xA0, 0x01, 0xA9, 0x55, 0x51, 0x10); Step(3); Assert.Equal(0xFF, Cpu.A); }

    // ── ADC / SBC additional modes ────────────────────────────────────────────

    [Fact]
    public void ADC_ZeroPageX() { Ram.Write(0x0005, 0x10); Load(0x0200, 0xA2, 0x02, 0x18, 0xA9, 0x05, 0x75, 0x03); Step(4); Assert.Equal(0x15, Cpu.A); }
    [Fact]
    public void ADC_AbsoluteX() { Ram.Write(0x0402, 0x20); Load(0x0200, 0xA2, 0x02, 0x18, 0xA9, 0x05, 0x7D, 0x00, 0x04); Step(4); Assert.Equal(0x25, Cpu.A); }
    [Fact]
    public void ADC_AbsoluteY() { Ram.Write(0x0402, 0x30); Load(0x0200, 0xA0, 0x02, 0x18, 0xA9, 0x05, 0x79, 0x00, 0x04); Step(4); Assert.Equal(0x35, Cpu.A); }
    [Fact]
    public void ADC_IndexedIndirectX() { Ram.Write(0x0024, 0x00); Ram.Write(0x0025, 0x05); Ram.Write(0x0500, 0x40); Load(0x0200, 0xA2, 0x04, 0x18, 0xA9, 0x01, 0x61, 0x20); Step(4); Assert.Equal(0x41, Cpu.A); }
    [Fact]
    public void ADC_IndirectIndexedY() { Ram.Write(0x0010, 0x00); Ram.Write(0x0011, 0x05); Ram.Write(0x0502, 0x50); Load(0x0200, 0xA0, 0x02, 0x18, 0xA9, 0x01, 0x71, 0x10); Step(4); Assert.Equal(0x51, Cpu.A); }

    [Fact]
    public void SBC_ZeroPageX() { Ram.Write(0x0005, 0x05); Load(0x0200, 0xA2, 0x02, 0x38, 0xA9, 0x10, 0xF5, 0x03); Step(4); Assert.Equal(0x0B, Cpu.A); }
    [Fact]
    public void SBC_AbsoluteX() { Ram.Write(0x0402, 0x05); Load(0x0200, 0xA2, 0x02, 0x38, 0xA9, 0x10, 0xFD, 0x00, 0x04); Step(4); Assert.Equal(0x0B, Cpu.A); }
    [Fact]
    public void SBC_AbsoluteY() { Ram.Write(0x0402, 0x05); Load(0x0200, 0xA0, 0x02, 0x38, 0xA9, 0x10, 0xF9, 0x00, 0x04); Step(4); Assert.Equal(0x0B, Cpu.A); }
    [Fact]
    public void SBC_IndexedIndirectX() { Ram.Write(0x0024, 0x00); Ram.Write(0x0025, 0x05); Ram.Write(0x0500, 0x05); Load(0x0200, 0xA2, 0x04, 0x38, 0xA9, 0x10, 0xE1, 0x20); Step(4); Assert.Equal(0x0B, Cpu.A); }
    [Fact]
    public void SBC_IndirectIndexedY() { Ram.Write(0x0010, 0x00); Ram.Write(0x0011, 0x05); Ram.Write(0x0502, 0x05); Load(0x0200, 0xA0, 0x02, 0x38, 0xA9, 0x10, 0xF1, 0x10); Step(4); Assert.Equal(0x0B, Cpu.A); }
    [Fact]
    public void SBC_BCD_SubtractsCorrectly() { Load(0x0200, 0xF8, 0x38, 0xA9, 0x20, 0xE9, 0x05); Step(4); Assert.Equal(0x15, Cpu.A); }

    // ── INC / DEC memory variants ─────────────────────────────────────────────

    [Fact]
    public void INC_ZeroPageX() { Ram.Write(0x0005, 0x09); Load(0x0200, 0xA2, 0x02, 0xF6, 0x03); Step(2); Assert.Equal(0x0A, Ram.Read(0x0005)); }
    [Fact]
    public void INC_Absolute() { Ram.Write(0x0400, 0x09); Load(0x0200, 0xEE, 0x00, 0x04); Step(); Assert.Equal(0x0A, Ram.Read(0x0400)); }
    [Fact]
    public void INC_AbsoluteX() { Ram.Write(0x0402, 0x09); Load(0x0200, 0xA2, 0x02, 0xFE, 0x00, 0x04); Step(2); Assert.Equal(0x0A, Ram.Read(0x0402)); }

    [Fact]
    public void DEC_ZeroPageX() { Ram.Write(0x0005, 0x0A); Load(0x0200, 0xA2, 0x02, 0xD6, 0x03); Step(2); Assert.Equal(0x09, Ram.Read(0x0005)); }
    [Fact]
    public void DEC_Absolute() { Ram.Write(0x0400, 0x0A); Load(0x0200, 0xCE, 0x00, 0x04); Step(); Assert.Equal(0x09, Ram.Read(0x0400)); }
    [Fact]
    public void DEC_AbsoluteX() { Ram.Write(0x0402, 0x0A); Load(0x0200, 0xA2, 0x02, 0xDE, 0x00, 0x04); Step(2); Assert.Equal(0x09, Ram.Read(0x0402)); }

    // ── CMP / CPX / CPY additional modes ─────────────────────────────────────

    [Fact]
    public void CMP_ZeroPageX() { Ram.Write(0x0005, 0x42); Load(0x0200, 0xA2, 0x02, 0xA9, 0x42, 0xD5, 0x03); Step(3); Assert.True(Cpu.Z); }
    [Fact]
    public void CMP_AbsoluteX() { Ram.Write(0x0402, 0x42); Load(0x0200, 0xA2, 0x02, 0xA9, 0x42, 0xDD, 0x00, 0x04); Step(3); Assert.True(Cpu.Z); }
    [Fact]
    public void CMP_AbsoluteY() { Ram.Write(0x0402, 0x42); Load(0x0200, 0xA0, 0x02, 0xA9, 0x42, 0xD9, 0x00, 0x04); Step(3); Assert.True(Cpu.Z); }
    [Fact]
    public void CMP_IndexedIndirectX() { Ram.Write(0x0024, 0x00); Ram.Write(0x0025, 0x05); Ram.Write(0x0500, 0x42); Load(0x0200, 0xA2, 0x04, 0xA9, 0x42, 0xC1, 0x20); Step(3); Assert.True(Cpu.Z); }
    [Fact]
    public void CMP_IndirectIndexedY() { Ram.Write(0x0010, 0x00); Ram.Write(0x0011, 0x05); Ram.Write(0x0501, 0x42); Load(0x0200, 0xA0, 0x01, 0xA9, 0x42, 0xD1, 0x10); Step(3); Assert.True(Cpu.Z); }
    [Fact]
    public void CPX_ZeroPage() { Ram.Write(0x0050, 0x42); Load(0x0200, 0xA2, 0x42, 0xE4, 0x50); Step(2); Assert.True(Cpu.Z); }
    [Fact]
    public void CPX_Absolute() { Ram.Write(0x0400, 0x42); Load(0x0200, 0xA2, 0x42, 0xEC, 0x00, 0x04); Step(2); Assert.True(Cpu.Z); }
    [Fact]
    public void CPY_ZeroPage() { Ram.Write(0x0050, 0x42); Load(0x0200, 0xA0, 0x42, 0xC4, 0x50); Step(2); Assert.True(Cpu.Z); }
    [Fact]
    public void CPY_Absolute() { Ram.Write(0x0400, 0x42); Load(0x0200, 0xA0, 0x42, 0xCC, 0x00, 0x04); Step(2); Assert.True(Cpu.Z); }

    // ── ASL / LSR / ROL / ROR additional modes ────────────────────────────────

    [Fact]
    public void ASL_ZeroPageX() { Ram.Write(0x0005, 0x01); Load(0x0200, 0xA2, 0x02, 0x16, 0x03); Step(2); Assert.Equal(0x02, Ram.Read(0x0005)); }
    [Fact]
    public void ASL_Absolute()  { Ram.Write(0x0400, 0x01); Load(0x0200, 0x0E, 0x00, 0x04); Step(); Assert.Equal(0x02, Ram.Read(0x0400)); }
    [Fact]
    public void ASL_AbsoluteX() { Ram.Write(0x0402, 0x01); Load(0x0200, 0xA2, 0x02, 0x1E, 0x00, 0x04); Step(2); Assert.Equal(0x02, Ram.Read(0x0402)); }

    [Fact]
    public void LSR_ZeroPageX() { Ram.Write(0x0005, 0x04); Load(0x0200, 0xA2, 0x02, 0x56, 0x03); Step(2); Assert.Equal(0x02, Ram.Read(0x0005)); }
    [Fact]
    public void LSR_Absolute()  { Ram.Write(0x0400, 0x04); Load(0x0200, 0x4E, 0x00, 0x04); Step(); Assert.Equal(0x02, Ram.Read(0x0400)); }
    [Fact]
    public void LSR_AbsoluteX() { Ram.Write(0x0402, 0x04); Load(0x0200, 0xA2, 0x02, 0x5E, 0x00, 0x04); Step(2); Assert.Equal(0x02, Ram.Read(0x0402)); }

    [Fact]
    public void ROL_ZeroPageX() { Ram.Write(0x0005, 0x01); Load(0x0200, 0x18, 0xA2, 0x02, 0x36, 0x03); Step(3); Assert.Equal(0x02, Ram.Read(0x0005)); }
    [Fact]
    public void ROL_Absolute()  { Ram.Write(0x0400, 0x01); Load(0x0200, 0x18, 0x2E, 0x00, 0x04); Step(2); Assert.Equal(0x02, Ram.Read(0x0400)); }
    [Fact]
    public void ROL_AbsoluteX() { Ram.Write(0x0402, 0x01); Load(0x0200, 0x18, 0xA2, 0x02, 0x3E, 0x00, 0x04); Step(3); Assert.Equal(0x02, Ram.Read(0x0402)); }

    [Fact]
    public void ROR_ZeroPageX() { Ram.Write(0x0005, 0x04); Load(0x0200, 0x18, 0xA2, 0x02, 0x76, 0x03); Step(3); Assert.Equal(0x02, Ram.Read(0x0005)); }
    [Fact]
    public void ROR_Absolute()  { Ram.Write(0x0400, 0x04); Load(0x0200, 0x18, 0x6E, 0x00, 0x04); Step(2); Assert.Equal(0x02, Ram.Read(0x0400)); }
    [Fact]
    public void ROR_AbsoluteX() { Ram.Write(0x0402, 0x04); Load(0x0200, 0x18, 0xA2, 0x02, 0x7E, 0x00, 0x04); Step(3); Assert.Equal(0x02, Ram.Read(0x0402)); }

    // ── LDA additional modes (ZP, Abs already covered in LoadStoreTests) ──────

    [Fact]
    public void LDA_AbsoluteY_SamePage_Costs4Cycles()
    {
        Ram.Write(0x0301, 0x77);
        Load(0x0200, 0xA0, 0x01, 0xB9, 0x00, 0x03); // LDY #$01, LDA $0300,Y
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(4UL, Cpu.TotalCycles - after);
        Assert.Equal(0x77, Cpu.A);
    }

    [Fact]
    public void LDA_ZeroPage_SetsNegative()
    {
        Ram.Write(0x0050, 0x80);
        Load(0x0200, 0xA5, 0x50); // LDA $50
        Step();
        Assert.True(Cpu.N);
    }

    [Fact]
    public void LDA_IndirectIndexedY_SamePage_Costs5Cycles()
    {
        Ram.Write(0x0010, 0x00); Ram.Write(0x0011, 0x03);
        Ram.Write(0x0302, 0x42);
        Load(0x0200, 0xA0, 0x02, 0xB1, 0x10); // LDY #$02, LDA ($10),Y → $0302
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(5UL, Cpu.TotalCycles - after);
        Assert.Equal(0x42, Cpu.A);
    }
}
