using Adapters.Raylib;
using Machines.Atom;

// ── argument parsing ──────────────────────────────────────────────────────────
string? basicPath = null;
string? osPath    = null;
string? tapePath  = null;
string? floatPath = null;
string? extPath   = null;
int     scale     = 3;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--basic": basicPath = args[++i]; break;
        case "--os":    osPath    = args[++i]; break;
        case "--tape":  tapePath  = args[++i]; break;
        case "--float": floatPath = args[++i]; break;
        case "--ext":   extPath   = args[++i]; break;
        case "--scale": scale     = int.Parse(args[++i]); break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 1;
    }
}

if (basicPath is null || osPath is null)
{
    Console.Error.WriteLine("--basic and --os are required.");
    PrintUsage();
    return 1;
}

// ── load ROMs ─────────────────────────────────────────────────────────────────
byte[] basicRom = File.ReadAllBytes(basicPath);
byte[] osRom    = File.ReadAllBytes(osPath);
byte[]? floatRom = floatPath is not null ? File.ReadAllBytes(floatPath) : null;
byte[]? extRom   = extPath   is not null ? File.ReadAllBytes(extPath)   : null;

// ── tape ──────────────────────────────────────────────────────────────────────
AtomTapeAdapter? tape = null;
if (tapePath is not null)
{
    tape = new AtomTapeAdapter();
    using var fs = File.OpenRead(tapePath);
    tape.LoadUef(fs);
    Console.WriteLine($"Tape loaded: {Path.GetFileName(tapePath)}");
}

// ── build machine and host ────────────────────────────────────────────────────
using var host = new RaylibHost("Acorn Atom", scale);

var machine = new AtomMachine(
    basicRom, osRom,
    keyboard: host,
    audio:    host,
    floatRom: floatRom,
    extRom:   extRom,
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
        Usage: atom --basic <path> --os <path> [options]

        Options:
          --tape  <path>   UEF tape image (.uef or .uef.gz)
          --float <path>   Floating-point ROM image
          --ext   <path>   Extension ROM image (#A socket)
          --scale <n>      Window scale factor (default: 3)
        """);
}
