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

var ram = new Ram(0x8000); // 32 KB main RAM ($0000–$7FFF)
var ula = new ElectronUla(basicRom: basicRom, osRom: osRom);
var bus = new AddressDecoder();

bus.Map(0x0000, 0x7FFF, ram);
bus.Map(0x8000, 0xFFFF, ula);

var cpu = new Cpu(bus);
cpu.Reset();

using var display = new RaylibHost("Acorn Electron", new DisplayOptions(scale: options.Scale, smooth: options.Smooth, scanlines: options.Scanlines), 640, 256);

const int cyclesPerFrame = 20_000;

while (display.IsRunning)
{
    display.PollEvents();
    
    ulong target = cpu.TotalCycles + cyclesPerFrame;
    while (cpu.TotalCycles < target)
    {
        cpu.Step();
    }
}

return 0;
