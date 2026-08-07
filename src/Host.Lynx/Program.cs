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

if (cartBytes is not null)
{
    Console.WriteLine($"Cartridge ROM: {options.CartPath} ({cartBytes.Length} bytes)");
}

var machine = new LynxMachine(cartridgeRom: cartBytes);
machine.Reset();

using var display = new RaylibHost("Atari Lynx Handheld", new DisplayOptions(Scale: options.Scale, Smooth: options.Smooth, ScanlineIntensity: options.Scanlines), 160, 102);

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame(display);
}

return 0;
