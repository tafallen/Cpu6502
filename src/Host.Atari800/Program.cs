using Adapters.Raylib;
using Machines.Atari800;

namespace Host.Atari800;

public static class Program
{
    public static void Main(string[] args)
    {
        var options = AtariCommandLine.Parse(args);

        byte[]? osRom = options.OsRomPath is not null && File.Exists(options.OsRomPath)
            ? File.ReadAllBytes(options.OsRomPath)
            : null;

        byte[]? basicRom = options.BasicRomPath is not null && File.Exists(options.BasicRomPath)
            ? File.ReadAllBytes(options.BasicRomPath)
            : null;

        var machine = new Atari800Machine(osRom, basicRom);

        if (options.XexPath is not null && File.Exists(options.XexPath))
        {
            byte[] xexData = File.ReadAllBytes(options.XexPath);
            ushort runAddr = AtariProgramLoader.LoadXex(xexData, machine.Bus);
            machine.Cpu.PC = runAddr;
        }

        using var host = new RaylibHost(
            title: "Atari 800XL Emulator (Machines.Atari800)",
            frameWidth: 336,
            frameHeight: 240,
            options: new DisplayOptions
            {
                Scale = options.Scale,
                Smooth = options.Smooth,
                ScanlineIntensity = options.Scanlines
            });

        machine.Reset();

        while (host.IsRunning)
        {
            host.PollEvents();
            machine.RunFrame(host);
        }
    }
}
