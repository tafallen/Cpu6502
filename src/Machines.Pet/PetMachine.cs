using Cpu6502.Core;
using Machines.Common;

namespace Machines.Pet;

/// <summary>
/// Master machine container for Commodore PET 2001 / 4032 / 8032 emulator.
/// Architecture:
/// - $0000–$7FFF: 32 KB Main RAM
/// - $8000–$87FF: 2 KB Video RAM
/// - $9000–$FFFF: 28 KB ROMs & MMIO ($E810 PIA 6520, $E840 VIA 6522)
/// </summary>
public sealed class PetMachine
{
    public Cpu Cpu { get; }
    public Ram Ram { get; }
    public Ram VideoRam { get; }
    public PetRom Rom { get; }
    public Pia6520 Pia { get; } = new();
    public Via6522 Via { get; } = new();
    public AddressDecoder Bus { get; }

    public const int CyclesPerFrame = 20_000; // 1 MHz / 50 Hz PAL

    public PetMachine(byte[] romData)
    {
        ArgumentNullException.ThrowIfNull(romData);

        Ram = new Ram(0x8000); // 32 KB RAM ($0000–$7FFF)
        VideoRam = new Ram(0x0800); // 2 KB Video RAM ($8000–$87FF)
        Rom = new PetRom(romData);

        Bus = new AddressDecoder();
        Bus.Map(0x0000, 0x7FFF, Ram);
        Bus.Map(0x8000, 0x87FF, VideoRam);
        Bus.Map(0x9000, 0xFFFF, Rom);
        Bus.Map(0xE810, 0xE813, Pia);
        Bus.Map(0xE840, 0xE84F, Via);

        Pia.ReadPortB = () => Keyboard.ScanRow(Pia.PortALatch);

        Cpu = new Cpu(Bus);
    }

    public PetVideo Video { get; } = new();
    public PetKeyboardAdapter Keyboard { get; } = new();
    public Ieee488Bus Ieee488 { get; } = new();

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
            Video.RenderFrame(VideoRam, sink);
        }
    }
}
