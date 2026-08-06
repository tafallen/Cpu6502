using Adapters.Raylib;
using Cpu6502.Core;
using Host.AcornSystem;
using Machines.AcornSystem;

AcornSystemOptions options;
try
{
    options = AcornSystemCommandLine.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error parsing command line: {ex.Message}");
    return 1;
}

Console.WriteLine($"Acorn {options.Model} Emulator");
Console.WriteLine($"System ROM: {options.RomPath}");

byte[] systemRom = File.Exists(options.RomPath) ? File.ReadAllBytes(options.RomPath) : new byte[0x2000];

var machine = new AcornSystemMachine(systemRom: systemRom, model: options.Model);
machine.Reset();

using var display = new RaylibHost($"Acorn {options.Model}", new DisplayOptions(Scale: options.Scale, Smooth: options.Smooth, ScanlineIntensity: options.ScanlineIntensity), 256, 192);

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame(display);
}

return 0;
