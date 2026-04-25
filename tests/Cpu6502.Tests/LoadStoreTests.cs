using Xunit;

namespace Cpu6502.Tests;

public class LoadStoreTests : CpuFixture
{
    // ── LDA ───────────────────────────────────────────────────────────────────

    [Fact]
    public void LDA_Immediate_LoadsValue()
    {
        Load(0x0200, 0xA9, 0x42); // LDA #$42
        Step();
        Assert.Equal(0x42, Cpu.A);
    }

    [Fact]
    public void LDA_Immediate_SetsZeroFlag_WhenValueIsZero()
    {
        Load(0x0200, 0xA9, 0x00); // LDA #$00
        Step();
        Assert.True(Cpu.Z);
        Assert.False(Cpu.N);
    }

    [Fact]
    public void LDA_Immediate_SetsNegativeFlag_WhenBit7Set()
    {
        Load(0x0200, 0xA9, 0x80); // LDA #$80
        Step();
        Assert.True(Cpu.N);
        Assert.False(Cpu.Z);
    }

    [Fact]
    public void LDA_Immediate_Costs2Cycles()
    {
        Load(0x0200, 0xA9, 0x42); // LDA #$42
        var before = CycleSnapshot();
        Step();
        Assert.Equal(2UL, Cpu.TotalCycles - before);
    }

    [Fact]
    public void LDA_ZeroPage_LoadsFromZeroPage()
    {
        Ram.Write(0x0042, 0x37);
        Load(0x0200, 0xA5, 0x42); // LDA $42
        Step();
        Assert.Equal(0x37, Cpu.A);
    }

    [Fact]
    public void LDA_ZeroPage_Costs3Cycles()
    {
        Load(0x0200, 0xA5, 0x00);
        var before = CycleSnapshot();
        Step();
        Assert.Equal(3UL, Cpu.TotalCycles - before);
    }

    [Fact]
    public void LDA_ZeroPageX_AddsXWithWrap()
    {
        Ram.Write(0x0001, 0xAB);   // $FF + X($02) wraps to $01
        Load(0x0200, 0xB5, 0xFF);  // LDA $FF,X  with X=2 → reads $01
        Cpu.Reset();
        Ram.Write(0xFFFC, 0x00); Ram.Write(0xFFFD, 0x02); Cpu.Reset();
        // Set X=2 via LDX
        Ram.Write(0x0200, 0xA2); Ram.Write(0x0201, 0x02); // LDX #$02
        Ram.Write(0x0202, 0xB5); Ram.Write(0x0203, 0xFF); // LDA $FF,X
        Step(2);
        Assert.Equal(0xAB, Cpu.A);
    }

    [Fact]
    public void LDA_Absolute_Costs4Cycles()
    {
        Load(0x0200, 0xAD, 0x00, 0x03); // LDA $0300
        var before = CycleSnapshot();
        Step();
        Assert.Equal(4UL, Cpu.TotalCycles - before);
    }

    [Fact]
    public void LDA_AbsoluteX_PageCross_Costs5Cycles()
    {
        // $00FF + X=1 → $0100 (page cross)
        Ram.Write(0x0100, 0x55);
        Load(0x0200,
            0xA2, 0x01,             // LDX #$01
            0xBD, 0xFF, 0x00);      // LDA $00FF,X
        var before = CycleSnapshot();
        Step();                     // LDX
        var afterLDX = CycleSnapshot();
        Step();                     // LDA AbsX (page cross)
        Assert.Equal(5UL, Cpu.TotalCycles - afterLDX);
        Assert.Equal(0x55, Cpu.A);
    }

    [Fact]
    public void LDA_AbsoluteX_SamePage_Costs4Cycles()
    {
        Ram.Write(0x0201, 0x77);
        Load(0x0200,
            0xA2, 0x01,             // LDX #$01
            0xBD, 0x00, 0x02);      // LDA $0200,X (no page cross → $0201)
        Step();                     // LDX
        var after = CycleSnapshot();
        Step();                     // LDA
        Assert.Equal(4UL, Cpu.TotalCycles - after);
    }

    [Fact]
    public void LDA_IndexedIndirectX()
    {
        // (Indirect,X): read pointer from ZP[operand+X], then read value
        Ram.Write(0x0024, 0x00);  // lo byte of address
        Ram.Write(0x0025, 0x03);  // hi byte → address = $0300
        Ram.Write(0x0300, 0xCC);
        Load(0x0200,
            0xA2, 0x04,           // LDX #$04
            0xA1, 0x20);          // LDA ($20,X) → ptr at $24/$25 → $0300 → $CC
        Step(2);
        Assert.Equal(0xCC, Cpu.A);
    }

    [Fact]
    public void LDA_IndexedIndirectX_Costs6Cycles()
    {
        Load(0x0200, 0xA2, 0x00, 0xA1, 0x00); // LDX #0, LDA ($00,X)
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(6UL, Cpu.TotalCycles - after);
    }

    [Fact]
    public void LDA_IndirectIndexedY_PageCross_Costs6Cycles()
    {
        // ($ZP),Y: ptr at ZP = $00FF, + Y=1 → $0100 (page cross)
        Ram.Write(0x0010, 0xFF);  // lo of base addr
        Ram.Write(0x0011, 0x00);  // hi of base addr → $00FF
        Ram.Write(0x0100, 0x99);
        Load(0x0200,
            0xA0, 0x01,           // LDY #$01
            0xB1, 0x10);          // LDA ($10),Y → base $00FF + 1 = $0100
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(6UL, Cpu.TotalCycles - after);
        Assert.Equal(0x99, Cpu.A);
    }

    // ── LDX ───────────────────────────────────────────────────────────────────

    [Fact]
    public void LDX_Immediate_LoadsValue()
    {
        Load(0x0200, 0xA2, 0x10); // LDX #$10
        Step();
        Assert.Equal(0x10, Cpu.X);
    }

    [Fact]
    public void LDX_Immediate_Costs2Cycles()
    {
        Load(0x0200, 0xA2, 0x00);
        var before = CycleSnapshot();
        Step();
        Assert.Equal(2UL, Cpu.TotalCycles - before);
    }

    // ── LDY ───────────────────────────────────────────────────────────────────

    [Fact]
    public void LDY_Immediate_LoadsValue()
    {
        Load(0x0200, 0xA0, 0x20); // LDY #$20
        Step();
        Assert.Equal(0x20, Cpu.Y);
    }

    // ── STA ───────────────────────────────────────────────────────────────────

    [Fact]
    public void STA_ZeroPage_StoresAccumulator()
    {
        Load(0x0200,
            0xA9, 0xAB,  // LDA #$AB
            0x85, 0x50); // STA $50
        Step(2);
        Assert.Equal(0xAB, Ram.Read(0x0050));
    }

    [Fact]
    public void STA_ZeroPage_DoesNotAffectFlags()
    {
        Load(0x0200,
            0xA9, 0x00,  // LDA #$00  (sets Z)
            0x85, 0x50); // STA $50
        Step(2);
        Assert.True(Cpu.Z);  // Z unchanged by STA
    }

    [Fact]
    public void STA_Absolute_Costs4Cycles()
    {
        Load(0x0200,
            0xA9, 0x01,        // LDA #$01
            0x8D, 0x00, 0x04); // STA $0400
        Step();
        var after = CycleSnapshot();
        Step();
        Assert.Equal(4UL, Cpu.TotalCycles - after);
    }

    [Fact]
    public void STA_AbsoluteX_AlwaysCosts5Cycles_NoPageCross()
    {
        Load(0x0200,
            0xA2, 0x01,        // LDX #$01
            0xA9, 0x77,        // LDA #$77
            0x9D, 0x00, 0x04); // STA $0400,X → $0401 (no page cross)
        Step(2);
        var after = CycleSnapshot();
        Step();
        Assert.Equal(5UL, Cpu.TotalCycles - after);
    }

    // ── STX / STY ─────────────────────────────────────────────────────────────

    [Fact]
    public void STX_Absolute_StoresX()
    {
        Load(0x0200,
            0xA2, 0xBB,        // LDX #$BB
            0x8E, 0x00, 0x04); // STX $0400
        Step(2);
        Assert.Equal(0xBB, Ram.Read(0x0400));
    }

    [Fact]
    public void STY_ZeroPage_StoresY()
    {
        Load(0x0200,
            0xA0, 0xCC,  // LDY #$CC
            0x84, 0x60); // STY $60
        Step(2);
        Assert.Equal(0xCC, Ram.Read(0x0060));
    }
}
