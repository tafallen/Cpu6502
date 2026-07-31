using Cpu6502.Core;
using Machines.BbcMicro;
using Machines.Common;

namespace Machines.BbcMaster;

/// <summary>
/// Master machine container for Acorn BBC Master 128 microcomputer target (1986).
/// High-performance implementation with cached VRAM pointers and batched VIA ticks.
/// </summary>
public sealed class BbcMasterMachine
{
    public Cpu Cpu { get; }
    public Ram MainRam { get; }
    public Ram ShadowRam { get; }
    public BbcMasterAcccon Acccon { get; } = new();
    public Mc146818Rtc Rtc { get; } = new();
    public Wd1770Fdc Fdc { get; } = new();
    public TubeUla Tube { get; } = new();
    public Mc6845 Crtc { get; } = new();
    public Saa5050 Teletext { get; } = new();
    public Via6522 SystemVia { get; } = new();
    public Via6522 UserVia { get; } = new();
    public AddressDecoder Bus { get; }

    private Ram _activeVideoRam;

    public const int CyclesPerFrame = 40_000; // 2.0 MHz / 50 Hz PAL

    public BbcMasterMachine(byte[] osRomData)
    {
        MainRam = new Ram(0x8000);   // 32 KB Main RAM ($0000–$7FFF)
        ShadowRam = new Ram(0x8000); // 32 KB Shadow RAM ($0000–$7FFF)
        _activeVideoRam = MainRam;

        Bus = new AddressDecoder();
        Bus.Map(0x0000, 0x7FFF, MainRam);
        Bus.Map(0xFE30, 0xFE31, Rtc);
        Bus.Map(0xFE34, 0xFE34, Acccon);
        Bus.Map(0xFE40, 0xFE4F, SystemVia);
        Bus.Map(0xFE60, 0xFE6F, UserVia);
        Bus.Map(0xFE80, 0xFE83, Fdc);
        Bus.Map(0xFEE0, 0xFEEF, Tube);

        if (osRomData.Length >= 0x4000)
        {
            var osRom = new BbcOsRom(osRomData);
            Bus.Map(0xC000, 0xFFFF, osRom);
        }

        Cpu = new Cpu(Bus);

        Acccon.OnAccconChanged += (val) =>
        {
            _activeVideoRam = Acccon.DisplayShadowSelect ? ShadowRam : MainRam;
            Ram executeRam = Acccon.MainRamSelect ? MainRam : ShadowRam;
            Bus.Map(0x0000, 0x7FFF, executeRam);
        };
    }

    public void Reset() => Cpu.Reset();

    public void Step()
    {
        ulong cyclesBefore = Cpu.TotalCycles;
        Cpu.Step();
        int delta = (int)(Cpu.TotalCycles - cyclesBefore);
        SystemVia.Tick(delta);
        UserVia.Tick(delta);

        if (SystemVia.Irq || UserVia.Irq)
        {
            Cpu.Irq();
        }
    }

    public void RunFrame(IVideoSink? sink = null)
    {
        ulong target = Cpu.TotalCycles + CyclesPerFrame;
        while (Cpu.TotalCycles < target)
        {
            Step();
        }

        if (sink is not null)
        {
            Teletext.RenderMode7(_activeVideoRam, 0x7C00, sink);
        }
    }
}
