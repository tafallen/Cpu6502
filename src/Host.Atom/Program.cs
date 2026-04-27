using Adapters.Raylib;
using Machines.Atom;

// ── argument parsing ──────────────────────────────────────────────────────────
string? basicPath = null;
string? osPath    = null;
string? tapePath  = null;
string? floatPath = null;
string? extPath   = null;
string? charPath  = null;
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
        case "--char":  charPath  = args[++i]; break;
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
byte[] basicRom  = File.ReadAllBytes(basicPath);
byte[] osRom     = File.ReadAllBytes(osPath);
byte[]? floatRom = floatPath is not null ? File.ReadAllBytes(floatPath) : null;
byte[]? extRom   = extPath   is not null ? File.ReadAllBytes(extPath)   : null;
byte[]? charRom  = charPath  is not null ? File.ReadAllBytes(charPath)  : null;

// ── tape ──────────────────────────────────────────────────────────────────────
AtomTapeAdapter? tape = null;
if (tapePath is not null)
{
    tape = new AtomTapeAdapter();
    using var fs = File.OpenRead(tapePath);
    tape.LoadUef(fs);
    Console.WriteLine($"Tape loaded: {Path.GetFileName(tapePath)}");
}

// ── diagnostics ───────────────────────────────────────────────────────────────
Console.WriteLine($"BASIC ROM:  {basicRom.Length} bytes");
Console.WriteLine($"OS ROM:     {osRom.Length} bytes");
if (floatRom is not null) Console.WriteLine($"Float ROM:  {floatRom.Length} bytes");
if (charRom  is not null) Console.WriteLine($"Char ROM:   {charRom.Length} bytes");

// Reset vector is at the last two bytes of the OS ROM ($FFFC/$FFFD)
// OS ROM is mapped at $F000, so $FFFC is at offset $FFC within the ROM
if (osRom.Length >= 0x1000)
{
    ushort resetVec = (ushort)(osRom[0xFFC] | (osRom[0xFFD] << 8));
    Console.WriteLine($"Reset vector: ${resetVec:X4}");
}
else
{
    Console.WriteLine($"WARNING: OS ROM is only {osRom.Length} bytes — expected 4096 ($1000)");
}

// ── build machine and host ────────────────────────────────────────────────────
using var host = new RaylibHost("Acorn Atom", scale);

var machine = new AtomMachine(
    basicRom, osRom,
    keyboard: host,
    audio:    host,
    floatRom: floatRom,
    extRom:   extRom,
    charRom:  charRom,
    tape:     tape);

machine.Reset();
Console.WriteLine($"PC after reset: ${machine.Cpu.PC:X4}");

// Trace 2000 instructions, printing only the first visit to each address.
// This compresses loops down to one line so we can see the full control flow.
Console.WriteLine("--- Instruction trace (first visit per address) ---");
var seen = new HashSet<ushort>();
for (int i = 0; i < 2000; i++)
{
    ushort pc  = machine.Cpu.PC;
    byte   op  = machine.Bus.Read(pc);
    byte   b1  = machine.Bus.Read((ushort)(pc + 1));
    byte   b2  = machine.Bus.Read((ushort)(pc + 2));
    bool   fresh = seen.Add(pc);
    machine.Cpu.Step();
    if (fresh)
        Console.WriteLine($"  ${pc:X4}: {op:X2} {b1:X2} {b2:X2}  → PC=${machine.Cpu.PC:X4}  A=${machine.Cpu.A:X2} X=${machine.Cpu.X:X2} Y=${machine.Cpu.Y:X2} SP=${machine.Cpu.SP:X2}");
}
Console.WriteLine("--- end trace ---");

// ── emulator loop ─────────────────────────────────────────────────────────────
int frameCount = 0;
while (host.IsRunning)
{
    host.PollEvents();
    machine.RunFrame();
    machine.RenderFrame(host);

    if (++frameCount == 5)
    {
        Console.Write("Video RAM (first 32 bytes): ");
        for (int i = 0; i < 32; i++)
            Console.Write($"{machine.VideoRam.Read((ushort)i):X2} ");
        Console.WriteLine();
        Console.WriteLine($"PC after 5 frames: ${machine.Cpu.PC:X4}  Cycles: {machine.Cpu.TotalCycles}");
    }
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
          --char  <path>   MC6847 character ROM (768 bytes; built-in default used if omitted)
          --scale <n>      Window scale factor (default: 3)
        """);
}
