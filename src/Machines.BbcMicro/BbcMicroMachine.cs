using Cpu6502.Core;
using Machines.Common;

namespace Machines.BbcMicro;

/// <summary>Acorn BBC Micro hardware model variant.</summary>
public enum BbcModel
{
    ModelA = 0,       // 16 KB RAM ($0000–$3FFF)
    ModelB = 1,       // 32 KB RAM ($0000–$7FFF)
    ModelBPlus64 = 2  // 64 KB RAM (32 KB Main RAM $0000–$7FFF + 32 KB Sideways RAM $8000–$BFFF)
}

/// <summary>
/// Master machine container for the Acorn BBC Micro emulator (Model A, Model B, and Model B+ 64K).
/// Composes Cpu, RAM (16 KB Model A, 32 KB Model B, or 64 KB Model B+), BbcSidewaysRomBank, OS ROM, and BbcSheilaBus.
/// </summary>
public sealed class BbcMicroMachine
{
    public Cpu Cpu { get; }
    public Ram Ram { get; }
    public BbcOsRom OsRom { get; }
    public BbcSidewaysRomBank SidewaysRomBank { get; }
    public BbcSheilaBus SheilaBus { get; }
    public AddressDecoder Bus { get; }
    public BbcModel Model { get; }

    public const int CyclesPerFrame = 40_000; // 2 MHz / 50 Hz PAL

    public TubeCoProcessor? CoProcessor { get; }

    public BbcMicroMachine(byte[] osRom, byte[]? basicRom = null, BbcModel model = BbcModel.ModelB, bool enableCoProcessor = false)
    {
        ArgumentNullException.ThrowIfNull(osRom);

        Model = model;
        int ramSize = model switch
        {
            BbcModel.ModelA => 0x4000,
            BbcModel.ModelB => 0x8000,
            BbcModel.ModelBPlus64 => 0x10000, // 64 KB Total RAM
            _ => 0x8000
        };

        ushort ramEndAddress = model == BbcModel.ModelA ? (ushort)0x3FFF : (ushort)0x7FFF;

        Ram = new Ram(ramSize);
        OsRom = new BbcOsRom(osRom);
        SidewaysRomBank = new BbcSidewaysRomBank();

        if (model == BbcModel.ModelBPlus64)
        {
            // Populate Sideways RAM banks 12–15 (16 KB) for Model B+
            byte[] sidewaysRam = new byte[0x4000];
            SidewaysRomBank.SetBankRom(12, sidewaysRam);
        }

        if (basicRom is not null && basicRom.Length > 0)
        {
            SidewaysRomBank.SetBankRom(15, basicRom); // Bank 15 = BBC BASIC
        }

        SheilaBus = new BbcSheilaBus(SidewaysRomBank);

        if (enableCoProcessor)
        {
            CoProcessor = new TubeCoProcessor(SheilaBus.TubeInterface);
        }

        Bus = new AddressDecoder();
        Bus.Map(0x0000, ramEndAddress, Ram);
        Bus.Map(0x8000, 0xBFFF, SidewaysRomBank);
        Bus.Map(0xC000, 0xFFFF, OsRom);
        Bus.Map(0xFC00, 0xFEFF, SheilaBus);

        Cpu = new Cpu(Bus);
    }

    public Saa5050 Teletext { get; } = new();
    public BbcKeyboardAdapter Keyboard { get; } = new();
    public Sn76489 Sound { get; } = new();
    public Bbc8271Fdc Fdc => SheilaBus.FdcController;

    public void Reset()
    {
        Cpu.Reset();
        CoProcessor?.Reset();
    }

    public void Step()
    {
        ulong cyclesBefore = Cpu.TotalCycles;
        Cpu.Step();
        int delta = (int)(Cpu.TotalCycles - cyclesBefore);
        SheilaBus.SystemViaController.Tick(delta);
        SheilaBus.UserViaController.Tick(delta);

        if (CoProcessor is not null)
        {
            // Step Parasite 65C02 CPU at 2× clock speed (4 MHz Turbo Tube)
            CoProcessor.Step();
            CoProcessor.Step();
        }

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
            Step();
        }
        if (sink is not null)
            RenderFrame(sink);
    }
}
