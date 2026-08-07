using Adapters.Raylib;
using Cpu6502.Core;
using Host.Lynx;
using Machines.Lynx;

LynxOptions options;
try
{
    options = LynxCommandLine.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error parsing command line: {ex.Message}");
    return 1;
}

Console.WriteLine("Atari Lynx Handheld Emulator");

byte[]? cartBytes = options.CartPath is not null && File.Exists(options.CartPath)
    ? File.ReadAllBytes(options.CartPath)
    : null;

byte[]? bootBytes = options.BootPath is not null && File.Exists(options.BootPath)
    ? File.ReadAllBytes(options.BootPath)
    : null;

if (bootBytes is not null)
{
    Console.WriteLine($"Boot ROM: {options.BootPath} ({bootBytes.Length} bytes)");
}

if (cartBytes is not null)
{
    Console.WriteLine($"Cartridge ROM: {options.CartPath} ({cartBytes.Length} bytes)");
}

var machine = new LynxMachine(cartridgeRom: cartBytes);

if (bootBytes is not null && bootBytes.Length >= 512)
{
    byte[]? ramBuf = machine.Ram.DirectWriteBuffer;
    if (ramBuf is not null)
    {
        Array.Copy(bootBytes, 0, ramBuf, 0xFE00, 512);
        ramBuf[0xFFFC] = 0x00;
        ramBuf[0xFFFD] = 0xFE;
    }
}

machine.Reset();

using var display = new RaylibHost("Atari Lynx Handheld", new DisplayOptions(Scale: options.Scale, Smooth: options.Smooth, ScanlineIntensity: options.Scanlines), 160, 102);

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame(display);
}

return 0;
