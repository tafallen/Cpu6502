using Cpu6502.Core;
using Machines.Common;

namespace Machines.Plus4;

/// <summary>Hardware model variant for Commodore 264 family.</summary>
public enum Plus4Model
{
    C16 = 0,   // 16 KB RAM ($0000–$3FFF)
    Plus4 = 1  // 64 KB RAM ($0000–$FDFF)
}

/// <summary>
/// Master machine container for Commodore Plus/4 & C16 emulator targets (1984).
/// Features 7501/8501 CPU (with $0001 I/O port banking), 16 KB/64 KB RAM, TED 7360 chip,
/// BASIC 3.5 ROM, and Kernal ROM.
/// </summary>
public sealed class Plus4Machine
{
    public Cpu Cpu { get; }
    public Ram Ram { get; }
    public Ted7360 Ted { get; }
    public AddressDecoder Bus { get; }
    public Plus4Model Model { get; }

    public const int CyclesPerFrame = 35_520; // ~1.78 MHz / 50 Hz PAL

    public Plus4Machine(byte[] kernalRom, byte[] basicRom, Plus4Model model = Plus4Model.Plus4)
    {
        ArgumentNullException.ThrowIfNull(kernalRom);
        ArgumentNullException.ThrowIfNull(basicRom);

        Model = model;
        int ramSize = model == Plus4Model.C16 ? 0x4000 : 0xFE00;
        ushort ramEndAddress = model == Plus4Model.C16 ? (ushort)0x3FFF : (ushort)0xFDFF;

        Ram = new Ram(ramSize);
        Ted = new Ted7360();

        Bus = new AddressDecoder();
        Bus.Map(0x0000, ramEndAddress, Ram);

        // Map TED 7360 registers at $FF00–$FF3F
        Bus.Map(0xFF00, 0xFF3F, Ted, baseAddress: 0xFF00);

        // Map Kernal ROM ($C000–$FFFF) and BASIC ROM ($8000–$BFFF)
        if (basicRom.Length >= 0x4000)
            Bus.Map(0x8000, 0xBFFF, new Rom(basicRom));

        if (kernalRom.Length >= 0x4000)
            Bus.Map(0xC000, 0xFFFF, new Rom(kernalRom));

        Cpu = new Cpu(Bus);
    }

    public void Reset() => Cpu.Reset();

    public void Step()
    {
        ulong cyclesBefore = Cpu.TotalCycles;
        Cpu.Step();
        int delta = (int)(Cpu.TotalCycles - cyclesBefore);

        Ted.Tick(delta);

        if (Ted.Irq)
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
            Ted.RenderFrame(Ram, sink);
        }
    }

    public static ushort LoadPrg(byte[] prgData, Plus4Machine machine)
    {
        if (prgData.Length < 3)
            throw new ArgumentException("Invalid PRG file format.");

        ushort loadAddr = (ushort)(prgData[0] | (prgData[1] << 8));
        int bytesToCopy = prgData.Length - 2;

        byte[]? ramBuf = machine.Ram.DirectWriteBuffer;
        if (ramBuf is not null && loadAddr + bytesToCopy <= ramBuf.Length)
        {
            Array.Copy(prgData, 2, ramBuf, loadAddr, bytesToCopy);
        }
        else
        {
            for (int i = 0; i < bytesToCopy; i++)
            {
                machine.Bus.Write((ushort)(loadAddr + i), prgData[2 + i]);
            }
        }

        return loadAddr;
    }
}
