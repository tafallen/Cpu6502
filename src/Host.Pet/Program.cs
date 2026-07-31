using Adapters.Raylib;
using Host.Pet;
using Machines.Pet;

PetOptions options;
try
{
    options = PetCommandLine.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error parsing command line: {ex.Message}");
    return 1;
}

Console.WriteLine("Commodore PET 2001 / 4032 / 8032 Emulator");
Console.WriteLine($"ROM: {options.RomPath}");

byte[] romData = File.Exists(options.RomPath) ? File.ReadAllBytes(options.RomPath) : new byte[0x7000];

var machine = new PetMachine(romData);
machine.Reset();

using var display = new RaylibHost("Commodore PET 2001", new DisplayOptions(scale: options.Scale, smooth: options.Smooth, scanlines: options.Scanlines), 320, 200);

while (display.IsRunning)
{
    display.PollEvents();
    machine.RunFrame(display);
}

return 0;
