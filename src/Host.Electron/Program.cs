using Adapters.Raylib;
using Cpu6502.Core;
using Host.Electron;
using Machines.Common;
using Machines.Electron;

ElectronOptions options;
try
{
    options = ElectronCommandLine.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error parsing command line: {ex.Message}");
    return 1;
}

Console.WriteLine("Acorn Electron Emulator");
Console.WriteLine($"OS ROM: {options.OsPath}");
Console.WriteLine($"BASIC ROM: {options.BasicPath}");

byte[] osRom = File.Exists(options.OsPath) ? File.ReadAllBytes(options.OsPath) : new byte[0x4000];
byte[] basicRom = File.Exists(options.BasicPath) ? File.ReadAllBytes(options.BasicPath) : new byte[0x4000];

var machine = new ElectronMachine(osRom: osRom, basicRom: basicRom);
machine.Reset();

using var display = new RaylibHost("Acorn Electron", new DisplayOptions(Scale: options.Scale, Smooth: options.Smooth, ScanlineIntensity: options.Scanlines), 640, 256);

const int cyclesPerFrame = 20_000;

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame(cyclesPerFrame);
}

return 0;
