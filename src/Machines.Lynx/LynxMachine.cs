using Cpu6502.Core;
using Machines.Common;

namespace Machines.Lynx;

/// <summary>
/// Master machine container for the Atari Lynx handheld console target (1989).
/// Features 65SC02 CPU core running at up to 4.0 MHz, 64 KB RAM ($0000–$FFF7),
/// MIKEY VLSI chip ($FD00–$FDFF), SUZY VLSI chip ($FC00–$FCFF), and Cartridge ROM reader.
/// </summary>
public sealed class LynxMachine
{
    public Cpu Cpu { get; }
    public Ram Ram { get; }
    public Mikey Mikey { get; }
    public Suzy Suzy { get; }
    public AddressDecoder Bus { get; }
    public Rom? BootRom { get; }

    public const int CyclesPerFrame = 80_000; // 4.0 MHz / 50 Hz PAL

    public LynxMachine(byte[]? cartridgeRom = null, byte[]? bootRom = null)
    {
        Cartridge? cart = null;
        if (cartridgeRom is not null && cartridgeRom.Length > 0)
        {
            cart = new Cartridge(cartridgeRom);
        }

        Ram = new Ram(0x10000); // 64 KB Full DRAM
        Mikey = new Mikey(null, cart);
        Suzy = new Suzy(cart);

        Bus = new AddressDecoder();
        Bus.Map(0x0000, 0xFFFF, Ram);

        if (bootRom is not null && bootRom.Length >= 512)
        {
            byte[] paddedBoot = new byte[512];
            Array.Copy(bootRom, 0, paddedBoot, 0, 512);
            BootRom = new Rom(paddedBoot);
            Bus.Map(0xFE00, 0xFFFF, BootRom, baseAddress: 0x0000);
        }

        Suzy.Ram = Ram;
        Suzy.Mikey = Mikey;

        // Map SUZY registers at $FC00–$FCFF
        Bus.Map(0xFC00, 0xFCFF, Suzy, baseAddress: 0xFC00);

        // Map MIKEY registers at $FD00–$FDFF
        Bus.Map(0xFD00, 0xFDFF, Mikey, baseAddress: 0xFD00);

        if (cartridgeRom is not null && cartridgeRom.Length > 0)
        {
            LoadCartridge(cartridgeRom, this);
        }

        Cpu = new Cpu(Bus);
        if (bootRom is not null && bootRom.Length >= 512)
        {
            // Set RESET vector at $FFFC/$FFFD to $FE00 to execute boot firmware
            byte[]? ramBuf = Ram.DirectWriteBuffer;
            if (ramBuf is not null)
            {
                ramBuf[0xFFFC] = 0x00;
                ramBuf[0xFFFD] = 0xFE;
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Reset() => Cpu.Reset();

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Step()
    {
        // If CPU PC is inside boot ROM RSA verification loop ($FE00–$FFF0), intercept loop and jump directly to game vector
        if (Cpu.PC >= 0xFE10 && Cpu.PC <= 0xFF00)
        {
            byte[]? ramBuf = Ram.DirectWriteBuffer;
            if (ramBuf is not null)
            {
                ushort gameVector = (ushort)(ramBuf[0x0206] | (ramBuf[0x0207] << 8));
                if (gameVector == 0 || gameVector >= 0xFE00) gameVector = 0x0200;
                Cpu.PC = gameVector;
            }
        }

        ulong cyclesBefore = Cpu.TotalCycles;
        Cpu.Step();
        int delta = (int)(Cpu.TotalCycles - cyclesBefore);

        Mikey.Tick(delta);

        if (Mikey.Irq)
        {
            Cpu.Irq();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void RunFrame(IVideoSink? sink = null)
    {
        ulong target = Cpu.TotalCycles + CyclesPerFrame;
        while (Cpu.TotalCycles < target)
        {
            Step();
        }

        if (sink is not null)
        {
            Mikey.RenderFrame(Ram, sink);
        }
    }

    public static void LoadCartridge(byte[] cartBytes, LynxMachine machine)
    {
        int headerOffset = 0;
        ushort loadAddress = 0x0200;

        // Check for 64-byte LNX header ("LYNX")
        if (cartBytes.Length >= 64 &&
            cartBytes[0] == 'L' && cartBytes[1] == 'Y' &&
            cartBytes[2] == 'N' && cartBytes[3] == 'X')
        {
            headerOffset = 64;
            // LNX header bytes 6..7 store execution load address
            ushort headerLoadAddr = (ushort)(cartBytes[6] | (cartBytes[7] << 8));
            if (headerLoadAddr != 0)
            {
                loadAddress = headerLoadAddr;
            }
        }

        int payloadLength = cartBytes.Length - headerOffset;
        byte[]? ramBuf = machine.Ram.DirectWriteBuffer;
        if (ramBuf is not null && payloadLength > 0)
        {
            int bytesToCopy = Math.Min(payloadLength, ramBuf.Length - loadAddress);
            Array.Copy(cartBytes, headerOffset, ramBuf, loadAddress, bytesToCopy);

            // Set RESET vector at $FFFC/$FFFD to game load address
            ramBuf[0xFFFC] = (byte)(loadAddress & 0xFF);
            ramBuf[0xFFFD] = (byte)(loadAddress >> 8);
        }
    }
}
