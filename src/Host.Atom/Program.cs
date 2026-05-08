using Adapters.Raylib;
using Host.Atom;
using Machines.Atom;

AtomOptions options;
try
{
    options = AtomCommandLine.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    PrintUsage();
    return 1;
}

// ── load ROMs ─────────────────────────────────────────────────────────────────
byte[] basicRom  = File.ReadAllBytes(options.BasicPath);
byte[] osRom     = File.ReadAllBytes(options.OsPath);
byte[]? floatRom = options.FloatPath is not null ? File.ReadAllBytes(options.FloatPath) : null;
byte[]? dosRom   = options.DosPath   is not null ? File.ReadAllBytes(options.DosPath)   : null;
byte[]? extRom   = options.ExtPath   is not null ? File.ReadAllBytes(options.ExtPath)   : null;
byte[]? charRom  = options.CharPath  is not null ? File.ReadAllBytes(options.CharPath)  : null;

// ── tape ──────────────────────────────────────────────────────────────────────
AtomTapeAdapter? tape = null;
if (options.TapePath is not null)
{
    tape = new AtomTapeAdapter();
    using var fs = File.OpenRead(options.TapePath);
    tape.LoadUef(fs);
    Console.WriteLine($"Tape loaded: {Path.GetFileName(options.TapePath)}");
}

// ── diagnostics ───────────────────────────────────────────────────────────────
Console.WriteLine($"BASIC ROM:  {basicRom.Length} bytes");
Console.WriteLine($"OS ROM:     {osRom.Length} bytes");
if (floatRom is not null) Console.WriteLine($"Float ROM:  {floatRom.Length} bytes");
if (dosRom   is not null) Console.WriteLine($"DOS ROM:    {dosRom.Length} bytes");
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
using var host = new RaylibHost(
    "Acorn Atom",
    new DisplayOptions(Scale: options.Scale, Smooth: options.Smooth, ScanlineIntensity: options.ScanlineIntensity),
    logKeypresses: options.DebugKeys);

var machine = new AtomMachine(
    basicRom, osRom,
    keyboard: host,
    audio:    host,
    floatRom: floatRom,
    dosRom:   dosRom,
    extRom:   extRom,
    charRom:  charRom,
    tape:     tape);

machine.Reset();
Console.WriteLine($"PC after reset: ${machine.Cpu.PC:X4}");

// Print IRQ vector so we know where the keyboard handler lives
if (osRom.Length >= 0x1000)
{
    ushort irqVec = (ushort)(osRom[0xFFE] | (osRom[0xFFF] << 8));
    Console.WriteLine($"IRQ vector:   ${irqVec:X4}");
}

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
          --tape       <path>   UEF tape image (.uef or .uef.gz)
          --float      <path>   Floating-point ROM (afloat.rom) — $D000-$DFFF
          --dos        <path>   DOS ROM (dosrom.rom) — $E000-$EFFF
          --ext        <path>   Utility ROM (#A socket, axr1.rom) — $A000-$AFFF
          --char       <path>   MC6847 character ROM (768 bytes; built-in default used if omitted)
          --scale      <n>      Window scale factor (default: 3)
          --smooth             Enable bilinear texture filtering (smooth scaling)
          --scanlines  <0..1>   CRT scanline intensity (0 = off, 0.5 = moderate, default 0)
          --debug-keys         Log raw keypresses from Raylib (debug only)
        """);
}
