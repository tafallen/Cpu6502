using Cpu6502.Core;
using Machines.Atom;
using Machines.Common;

namespace Machines.AcornSystem;

/// <summary>Hardware model variant for Acorn System 1–5 microcomputers.</summary>
public enum AcornSystemModel
{
    System1 = 0, // 512 bytes RAM ($0000–$01FF), 512B CUTS OS ROM ($FE00–$FFFF), 7-seg LED display & keypad
    System2 = 1, // 1 KB RAM ($0000–$03FF), 2 KB Cassette/Keypad OS ROM ($F800–$FFFF)
    System3 = 2, // 16 KB RAM ($0000–$3FFF), MC6847 VDG card ($8000–$9FFF), FDC controller
    System4 = 3, // 32 KB RAM ($0000–$7FFF), dual 5.25" floppy drives
    System5 = 4  // 48 KB RAM ($0000–$BFFF), 8" floppy drives
}

/// <summary>
/// Master machine container for Acorn System 1 / System 2 / System 3 / System 4 / System 5 microcomputer target (1979–1982).
/// Features 6502 CPU, Eurocard modular RAM, MC6847 VDG card, 8255 PPI I/O card, and 8271 FDC floppy controller.
/// </summary>
public sealed class AcornSystemMachine
{
    public Cpu Cpu { get; }
    public Ram Ram { get; }
    public Rom SystemRom { get; }
    public Mc6847 Vdg { get; }
    public AddressDecoder Bus { get; }
    public AcornSystemModel Model { get; }

    public const int CyclesPerFrame = 20_000; // 1.0 MHz / 50 Hz PAL

    public AcornSystemMachine(byte[] systemRom, AcornSystemModel model = AcornSystemModel.System3)
    {
        ArgumentNullException.ThrowIfNull(systemRom);

        Model = model;
        int ramSize = model switch
        {
            AcornSystemModel.System1 => 0x0200, // 512 bytes RAM ($0000–$01FF)
            AcornSystemModel.System2 => 0x0400, // 1 KB RAM ($0000–$03FF)
            AcornSystemModel.System3 => 0x4000, // 16 KB RAM ($0000–$3FFF)
            AcornSystemModel.System4 => 0x8000, // 32 KB RAM ($0000–$7FFF)
            AcornSystemModel.System5 => 0xC000, // 48 KB RAM ($0000–$BFFF)
            _ => 0x4000
        };

        ushort ramEndAddress = (ushort)(ramSize - 1);

        Ram = new Ram(ramSize);
        SystemRom = new Rom(systemRom);

        Bus = new AddressDecoder();
        Bus.Map(0x0000, ramEndAddress, Ram);

        // Map MC6847 VDG card Video RAM at $8000–$9FFF (if beyond main RAM)
        if (ramEndAddress < 0x8000)
        {
            var vram = new Ram(0x2000);
            Bus.Map(0x8000, 0x9FFF, vram, baseAddress: 0x8000);
            Vdg = new Mc6847(vram.Memory, vramBase: 0x8000);
        }
        else
        {
            Vdg = new Mc6847(Ram.Memory, vramBase: 0x8000);
        }

        // Map System ROM based on model variant
        ushort romStartAddress = model switch
        {
            AcornSystemModel.System1 => 0xFE00,
            AcornSystemModel.System2 => 0xF800,
            _ => 0xE000
        };

        int expectedRomSize = 0x10000 - romStartAddress;
        byte[] romBuf = new byte[expectedRomSize];
        Array.Copy(systemRom, 0, romBuf, 0, Math.Min(systemRom.Length, expectedRomSize));

        SystemRom = new Rom(romBuf);
        Bus.Map(romStartAddress, 0xFFFF, SystemRom);

        Cpu = new Cpu(Bus);
    }

    public void Reset() => Cpu.Reset();

    public void Step()
    {
        Cpu.Step();
    }

    public void RunFrame(IVideoSink? sink = null)
    {
        ulong target = Cpu.TotalCycles + CyclesPerFrame;
        while (Cpu.TotalCycles < target)
        {
            Cpu.Step();
        }

        if (sink is not null)
        {
            Vdg.RenderFrame(sink);
        }
    }
}
