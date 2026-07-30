using Adapters.Raylib;
using Host.BbcMicro;
using Machines.BbcMicro;

BbcMicroOptions options;
try
{
    options = BbcMicroCommandLine.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error parsing command line: {ex.Message}");
    return 1;
}

Console.WriteLine("Acorn BBC Micro Model B Emulator");
Console.WriteLine($"OS ROM: {options.OsPath}");
Console.WriteLine($"BASIC ROM: {options.BasicPath}");

byte[] osRom = File.Exists(options.OsPath) ? File.ReadAllBytes(options.OsPath) : new byte[0x4000];
byte[] basicRom = File.Exists(options.BasicPath) ? File.ReadAllBytes(options.BasicPath) : new byte[0x4000];

var machine = new BbcMicroMachine(osRom: osRom, basicRom: basicRom);
machine.Reset();

using var display = new RaylibHost("Acorn BBC Micro Model B", new DisplayOptions(scale: options.Scale, smooth: options.Smooth, scanlines: options.Scanlines), 640, 256);

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame(display);
}

return 0;
