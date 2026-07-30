using Cpu6502.Core;
using Machines.Common;

namespace Machines.BbcMicro;

/// <summary>
/// Master machine container for the Acorn BBC Micro Model B emulator.
/// Composes Cpu, 32 KB RAM, BbcSidewaysRomBank, OS 1.20 ROM, and BbcSheilaBus.
/// </summary>
public sealed class BbcMicroMachine
{
    public Cpu Cpu { get; }
    public Ram Ram { get; }
    public Rom OsRom { get; }
    public BbcSidewaysRomBank SidewaysRomBank { get; }
    public BbcSheilaBus SheilaBus { get; }
    public AddressDecoder Bus { get; }

    public const int CyclesPerFrame = 40_000; // 2 MHz / 50 Hz PAL

    public BbcMicroMachine(byte[] osRom, byte[]? basicRom = null)
    {
        ArgumentNullException.ThrowIfNull(osRom);

        byte[] osCopy = osRom.Length == 0x4000 ? osRom : new byte[0x4000];
        if (osRom.Length > 0 && osRom.Length <= 0x4000)
            Array.Copy(osRom, osCopy, osRom.Length);

        Ram = new Ram(0x8000); // 32 KB Main RAM ($0000–$7FFF)
        OsRom = new Rom(osCopy);
        SidewaysRomBank = new BbcSidewaysRomBank();

        if (basicRom is not null && basicRom.Length > 0)
        {
            SidewaysRomBank.SetBankRom(15, basicRom); // Bank 15 = BBC BASIC
        }

        SheilaBus = new BbcSheilaBus(SidewaysRomBank);

        Bus = new AddressDecoder();
        Bus.Map(0x0000, 0x7FFF, Ram);
        Bus.Map(0x8000, 0xBFFF, SidewaysRomBank);
        Bus.Map(0xC000, 0xFBFF, OsRom);
        Bus.Map(0xFC00, 0xFEFF, SheilaBus);
        Bus.Map(0xFF00, 0xFFFF, OsRom);

        Cpu = new Cpu(Bus);
    }

    public Saa5050 Teletext { get; } = new();
    public BbcKeyboardAdapter Keyboard { get; } = new();
    public Sn76489 Sound { get; } = new();

    public void Reset() => Cpu.Reset();

    public void Step()
    {
        ulong cyclesBefore = Cpu.TotalCycles;
        Cpu.Step();
        int delta = (int)(Cpu.TotalCycles - cyclesBefore);
        SheilaBus.SystemViaController.Tick(delta);
        SheilaBus.UserViaController.Tick(delta);

        if (SheilaBus.SystemViaController.Irq || SheilaBus.UserViaController.Irq)
        {
            Cpu.Irq();
        }
    }

    public void RenderFrame(IVideoSink sink)
    {
        Teletext.RenderMode7(Ram, 0x7C00, sink);
    }

    public void RunFrame(IVideoSink? sink = null)
    {
        ulong target = Cpu.TotalCycles + CyclesPerFrame;
        while (Cpu.TotalCycles < target)
        {
            Cpu.Step();
        }
        if (sink is not null)
            RenderFrame(sink);
    }
}
