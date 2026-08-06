using Cpu6502.Core;
using Machines.Common;

namespace Machines.BbcMicro;

/// <summary>
/// Acorn 65C02 Tube Co-Processor Card (3 MHz / 4 MHz Turbo 65C02 Second Processor).
/// Features secondary 65C02 CPU, 64 KB dedicated Parasite RAM ($0000–$FFFF), and Tube ULA interface.
/// Plugs into the BBC Micro Tube socket ($FEE0–$FEEF).
/// </summary>
public sealed class TubeCoProcessor
{
    public Cpu Cpu { get; }
    public Ram ParasiteRam { get; }
    public TubeUla TubeUla { get; }
    public AddressDecoder ParasiteBus { get; }

    public TubeCoProcessor(TubeUla tubeUla, byte[]? bootRom = null)
    {
        ArgumentNullException.ThrowIfNull(tubeUla);

        TubeUla = tubeUla;
        ParasiteRam = new Ram(0x10000); // 64 KB Dedicated Co-Processor RAM

        ParasiteBus = new AddressDecoder();
        ParasiteBus.Map(0x0000, 0xFFFF, ParasiteRam);

        // Map Tube ULA in Parasite memory space ($FEF0–$FEFF)
        ParasiteBus.Map(0xFEF0, 0xFEFF, TubeUla, baseAddress: 0xFEF0);

        if (bootRom is not null && bootRom.Length > 0)
        {
            int loadAddr = Math.Max(0, 0x10000 - bootRom.Length);
            byte[]? ramBuf = ParasiteRam.DirectWriteBuffer;
            if (ramBuf is not null)
            {
                Array.Copy(bootRom, 0, ramBuf, loadAddr, bootRom.Length);
            }
        }

        Cpu = new Cpu(ParasiteBus);
    }

    public void Reset() => Cpu.Reset();

    public void Step()
    {
        Cpu.Step();
    }
}
