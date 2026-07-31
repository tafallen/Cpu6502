using Adapters.Raylib;
using Host.BbcMaster;
using Machines.BbcMaster;

BbcMasterOptions options;
try
{
    options = BbcMasterCommandLine.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error parsing command line: {ex.Message}");
    return 1;
}

Console.WriteLine("Acorn BBC Master 128 Microcomputer Emulator");
Console.WriteLine($"MOS ROM: {options.OsPath}");

byte[] osRom = File.Exists(options.OsPath) ? File.ReadAllBytes(options.OsPath) : new byte[0x4000];

var machine = new BbcMasterMachine(osRom);
machine.Reset();

using var display = new RaylibHost("BBC Master 128", new DisplayOptions(scale: options.Scale, smooth: options.Smooth, scanlines: options.Scanlines), 640, 256);

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame(display);
}

return 0;
