using Adapters.Raylib;
using Cpu6502.Core;
using Host.Communicator;
using Machines.Communicator;

CommunicatorOptions options;
try
{
    options = CommunicatorCommandLine.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error parsing command line: {ex.Message}");
    return 1;
}

Console.WriteLine("Acorn Communicator Emulator");
Console.WriteLine($"ROM Path: {options.RomPath}");

byte[] systemRom = File.Exists(options.RomPath) ? File.ReadAllBytes(options.RomPath) : new byte[0x8000];

var machine = new CommunicatorMachine(systemRom: systemRom);
machine.Reset();

using var display = new RaylibHost("Acorn Communicator", new DisplayOptions(Scale: options.Scale, Smooth: options.Smooth, ScanlineIntensity: options.Scanlines), 640, 256);

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame();
}

return 0;
