using Cpu6502.Core;
using Machines.Common;

namespace Machines.Oric;

/// <summary>
/// Master machine container for Oric-1 / Oric Atmos emulator.
/// Architecture:
/// - $0000–$BFDF: 48 KB RAM
/// - $0300–$030F: MOS 6522 VIA MMIO
/// - $C000–$FFFF: 16 KB BASIC/OS ROM
/// </summary>
public sealed class OricMachine
{
    public Cpu Cpu { get; }
    public Ram Ram { get; }
    public OricOsRom OsRom { get; }
    public Via6522 Via { get; } = new();
    public AddressDecoder Bus { get; }

    public const int CyclesPerFrame = 20_000; // 1 MHz / 50 Hz PAL

    public OricMachine(byte[] osRom)
    {
        ArgumentNullException.ThrowIfNull(osRom);

        Ram = new Ram(0xC000); // 48 KB RAM ($0000–$BFFF)
        OsRom = new OricOsRom(osRom);

        Bus = new AddressDecoder();
        Bus.Map(0x0000, 0xBFFF, Ram);
        Bus.Map(0x0300, 0x030F, Via);
        Bus.Map(0xC000, 0xFFFF, OsRom);

        Via.ReadPortB = () => Keyboard.ScanColumn(Via.PortALatch);

        Cpu = new Cpu(Bus);
    }

    public OricUlaVideo Video { get; } = new();
    public OricKeyboardAdapter Keyboard { get; } = new();

    public void Reset() => Cpu.Reset();

    public void Step()
    {
        ulong cyclesBefore = Cpu.TotalCycles;
        Cpu.Step();
        int delta = (int)(Cpu.TotalCycles - cyclesBefore);
        Via.Tick(delta);

        if (Via.Irq)
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
            Video.RenderFrame(Ram, sink);
        }
    }
}
