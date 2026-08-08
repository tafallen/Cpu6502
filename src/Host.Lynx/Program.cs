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

var machine = new LynxMachine(cartridgeRom: cartBytes, bootRom: bootBytes);

machine.Reset();

using var display = new RaylibHost("Atari Lynx Handheld", new DisplayOptions(Scale: options.Scale, Smooth: options.Smooth, ScanlineIntensity: options.Scanlines), 160, 102);

int frameCounter = 0;
long lastLogTime = DateTime.UtcNow.Ticks;

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame(display);

    frameCounter++;
    long now = DateTime.UtcNow.Ticks;
    if (now - lastLogTime >= TimeSpan.TicksPerSecond)
    {
        lastLogTime = now;
        ushort dispAddr = (ushort)(machine.Mikey.Read(0x94) | (machine.Mikey.Read(0x95) << 8));
        Console.WriteLine($"[Lynx Diagnostics] Frame: {frameCounter:D5} | CPU PC: 0x{machine.Cpu.PC:X4} | Cycles: {machine.Cpu.TotalCycles:N0} | DISPADDR: 0x{dispAddr:X4}");
    }
}

return 0;
