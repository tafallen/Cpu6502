using Adapters.Raylib;
using Host.C64;
using Machines.C64;

C64Options options;
try
{
    options = C64CommandLine.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error parsing command line: {ex.Message}");
    return 1;
}

Console.WriteLine("Commodore 64 Microcomputer Emulator");
Console.WriteLine($"KERNAL ROM: {options.KernalPath}");

byte[] kernal = File.Exists(options.KernalPath) ? File.ReadAllBytes(options.KernalPath) : new byte[0x2000];
byte[] basic  = File.Exists(options.BasicPath)  ? File.ReadAllBytes(options.BasicPath)  : new byte[0x2000];
byte[] chgen  = File.Exists(options.CharPath)   ? File.ReadAllBytes(options.CharPath)   : new byte[0x1000];

var machine = new C64Machine(kernal, basic, chgen);
machine.Reset();

using var display = new RaylibHost("Commodore 64", new DisplayOptions(Scale: options.Scale, Smooth: options.Smooth, ScanlineIntensity: options.Scanlines), 384, 272);

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame(display);
}

return 0;
