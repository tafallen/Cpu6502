using Adapters.Raylib;
using Host.Oric;
using Machines.Oric;

OricOptions options;
try
{
    options = OricCommandLine.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error parsing command line: {ex.Message}");
    return 1;
}

Console.WriteLine("Oric-1 / Oric Atmos Emulator");
Console.WriteLine($"ROM: {options.OsPath}");

byte[] osRom = File.Exists(options.OsPath) ? File.ReadAllBytes(options.OsPath) : new byte[0x4000];

var machine = new OricMachine(osRom);
machine.Reset();

using var display = new RaylibHost("Oric Atmos", new DisplayOptions(scale: options.Scale, smooth: options.Smooth, scanlines: options.Scanlines), 240, 200);

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame(display);
}

return 0;
