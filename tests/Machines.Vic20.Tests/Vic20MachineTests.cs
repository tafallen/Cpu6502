using Machines.Common;
using Machines.Vic20;

namespace Machines.Vic20.Tests;

public class Vic20MachineTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static byte[] MakeBasicRom()
    {
        var rom = new byte[0x2000];
        Array.Fill(rom, (byte)0xEA);
        return rom;
    }

    // Kernal ROM with reset vector at $FFFC/$FFFD → $0400
    private static byte[] MakeKernalRom(ushort resetVec = 0x0400)
    {
        var rom = new byte[0x2000];
        Array.Fill(rom, (byte)0xEA);
        rom[0x1FFC] = (byte)(resetVec & 0xFF);
        rom[0x1FFD] = (byte)(resetVec >> 8);
        return rom;
    }

    private static Vic20Machine Make(Vic20TapeAdapter? tape = null, IPhysicalKeyboard? kb = null) =>
        new(MakeBasicRom(), MakeKernalRom(), keyboard: kb, tape: tape);

    // ── reset & PC ───────────────────────────────────────────────────────────

    [Fact]
    public void Reset_SetsPcFromKernalVector()
    {
        var m = Make();
        m.Reset();
        Assert.Equal(0x0400, m.Cpu.PC);
    }

    // ── RAM ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Ram_ZeroPage_ReadWrite()
    {
        var m = Make();
        m.Ram.Write(0x0010, 0xAB);
        Assert.Equal(0xAB, m.Ram.Read(0x0010));
    }

    [Fact]
    public void Ram_MainArea_ReadWrite_At1000()
    {
        var m = Make();
        m.Ram.Write(0x1000, 0x55);
        Assert.Equal(0x55, m.Ram.Read(0x1000));
    }

    // ── ROM ──────────────────────────────────────────────────────────────────

    [Fact]
    public void BasicRom_VisibleAt_A000()
    {
        var basic = MakeBasicRom();
        basic[0] = 0x77;
        var m = new Vic20Machine(basic, MakeKernalRom());
        m.Reset();
        Assert.Equal(0x77, m.Bus.Read(0xA000));
    }

    [Fact]
    public void KernalRom_VisibleAt_E000()
    {
        var kernal = MakeKernalRom();
        kernal[0] = 0x99;
        var m = new Vic20Machine(MakeBasicRom(), kernal);
        m.Reset();
        Assert.Equal(0x99, m.Bus.Read(0xE000));
    }

    [Fact]
    public void WriteToRom_IsIgnored()
    {
        var m = Make();
        m.Reset();
        byte before = m.Bus.Read(0xA000);
        m.Bus.Write(0xA000, 0xFF);
        Assert.Equal(before, m.Bus.Read(0xA000));
    }

    // ── colour RAM ────────────────────────────────────────────────────────────

    [Fact]
    public void ColorRam_ReadWrite_At8000()
    {
        var m = Make();
        m.Bus.Write(0x8000, 0x0E);
        Assert.Equal(0x0E, m.Bus.Read(0x8000) & 0x0F);
    }

    // ── VIA 2 keyboard scan ───────────────────────────────────────────────────

    [Fact]
    public void Via2_PortA_NoKeysDown_Returns0xFF()
    {
        var m = Make();
        m.Reset();
        m.Bus.Write(0x9122, 0xFF); // DDRB = all outputs
        m.Bus.Write(0x9120, 0xFE); // select column 0 (active low)
        Assert.Equal(0xFF, m.Bus.Read(0x9121)); // Port A all high = no keys
    }

    // ── VIA 1 registers accessible ───────────────────────────────────────────

    [Fact]
    public void Via1_IerReadable_At911E()
    {
        var m = Make();
        m.Reset();
        m.Bus.Write(0x911E, 0x80 | 0x40); // enable T1 interrupt
        // IER read: bit 7 always 1, plus T1 enable (bit 6)
        Assert.Equal(0x80 | 0x40, m.Bus.Read(0x911E));
    }

    // ── RunFrame smoke test ───────────────────────────────────────────────────

    [Fact]
    public void RunFrame_AdvancesCpuCycles()
    {
        var m = Make();
        m.Reset();
        // NOP loop at $0400
        m.Ram.Write(0x0400, 0xEA); // NOP
        m.Ram.Write(0x0401, 0x4C); // JMP $0400
        m.Ram.Write(0x0402, 0x00);
        m.Ram.Write(0x0403, 0x04);
        m.RunFrame();
        Assert.True(m.Cpu.TotalCycles > 0);
    }

    // ── tape adapter ─────────────────────────────────────────────────────────

    [Fact]
    public void TapeProperty_ReturnsSuppliedAdapter()
    {
        var tape = new Vic20TapeAdapter();
        var m = Make(tape: tape);
        Assert.Same(tape, m.Tape);
    }

    [Fact]
    public void NoTape_TapePropertyIsNull()
    {
        var m = Make();
        Assert.Null(m.Tape);
    }
}
