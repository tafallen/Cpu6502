using Adapters.Raylib;
using Host.Vic20;
using Machines.Vic20;

Vic20Options options;
try
{
    options = Vic20CommandLine.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    PrintUsage();
    return 1;
}

// ── load ROMs ─────────────────────────────────────────────────────────────────
byte[] basicRom  = File.ReadAllBytes(options.BasicPath);
byte[] kernalRom = File.ReadAllBytes(options.KernalPath);
byte[]? charRom  = options.CharPath is not null ? File.ReadAllBytes(options.CharPath) : null;

// ── tape ──────────────────────────────────────────────────────────────────────
Vic20TapeAdapter? tape = null;
if (options.TapePath is not null)
{
    tape = new Vic20TapeAdapter();
    using var fs = File.OpenRead(options.TapePath);
    tape.LoadTap(fs);
    Console.WriteLine($"Tape loaded: {Path.GetFileName(options.TapePath)}");
}

// ── build machine and host ────────────────────────────────────────────────────
using var host = new RaylibHost("Commodore VIC-20", options.Scale, VicI.FrameWidth, VicI.FrameHeight, logKeypresses: options.DebugKeys);

var machine = new Vic20Machine(
    basicRom, kernalRom,
    charRom:  charRom,
    keyboard: host,
    audio:    host,
    tape:     tape);

machine.Reset();

// ── emulator loop ─────────────────────────────────────────────────────────────
while (host.IsRunning)
{
    host.PollEvents();
    machine.RunFrame();
    machine.RenderFrame(host);
}

return 0;

static void PrintUsage()
{
    Console.Error.WriteLine("""
        Usage: vic20 --basic <path> --kernal <path> [options]

        Options:
          --char  <path>   Character ROM image (4KB)
          --tape  <path>   TAP tape image
          --scale <n>      Window scale factor (default: 3)
          --debug-keys     Log raw keypresses from Raylib (debug only)
        """);
}
