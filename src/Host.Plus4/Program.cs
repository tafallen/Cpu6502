using Adapters.Raylib;
using Cpu6502.Core;
using Host.Plus4;
using Machines.Plus4;

Plus4Options options;
try
{
    options = Plus4CommandLine.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error parsing command line: {ex.Message}");
    return 1;
}

Console.WriteLine($"Commodore {options.Model} Emulator");
Console.WriteLine($"Kernal ROM: {options.KernalPath}");
Console.WriteLine($"BASIC ROM: {options.BasicPath}");

byte[] kernalRom = File.Exists(options.KernalPath) ? File.ReadAllBytes(options.KernalPath) : new byte[0x4000];
byte[] basicRom = File.Exists(options.BasicPath) ? File.ReadAllBytes(options.BasicPath) : new byte[0x4000];

var machine = new Plus4Machine(kernalRom: kernalRom, basicRom: basicRom, model: options.Model);

if (options.PrgPath is not null && File.Exists(options.PrgPath))
{
    byte[] prgBytes = File.ReadAllBytes(options.PrgPath);
    ushort startAddr = Plus4Machine.LoadPrg(prgBytes, machine);
    Console.WriteLine($"Loaded PRG: {Path.GetFileName(options.PrgPath)} at 0x{startAddr:X4}");
}

machine.Reset();

using var display = new RaylibHost($"Commodore {options.Model}", new DisplayOptions(Scale: options.Scale, Smooth: options.Smooth, ScanlineIntensity: options.ScanlineIntensity), 320, 200);

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame(display);
}

return 0;
