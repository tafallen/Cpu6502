using Adapters.Raylib;
using Machines.Vic20;

// ── argument parsing ──────────────────────────────────────────────────────────
string? basicPath  = null;
string? kernalPath = null;
string? charPath   = null;
string? tapePath   = null;
int     scale      = 3;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--basic":  basicPath  = args[++i]; break;
        case "--kernal": kernalPath = args[++i]; break;
        case "--char":   charPath   = args[++i]; break;
        case "--tape":   tapePath   = args[++i]; break;
        case "--scale":  scale      = int.Parse(args[++i]); break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 1;
    }
}

if (basicPath is null || kernalPath is null)
{
    Console.Error.WriteLine("--basic and --kernal are required.");
    PrintUsage();
    return 1;
}

// ── load ROMs ─────────────────────────────────────────────────────────────────
byte[] basicRom  = File.ReadAllBytes(basicPath);
byte[] kernalRom = File.ReadAllBytes(kernalPath);
byte[]? charRom  = charPath is not null ? File.ReadAllBytes(charPath) : null;

// ── tape ──────────────────────────────────────────────────────────────────────
Vic20TapeAdapter? tape = null;
if (tapePath is not null)
{
    tape = new Vic20TapeAdapter();
    using var fs = File.OpenRead(tapePath);
    tape.LoadTap(fs);
    Console.WriteLine($"Tape loaded: {Path.GetFileName(tapePath)}");
}

// ── build machine and host ────────────────────────────────────────────────────
using var host = new RaylibHost("Commodore VIC-20", scale);

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
        """);
}
