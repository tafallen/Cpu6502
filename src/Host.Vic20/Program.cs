using System;
using Adapters.Gdb;
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

// ── load cartridge ─────────────────────────────────────────────────────────────
byte[]? cartridgeRom = options.CartPath is not null ? File.ReadAllBytes(options.CartPath) : null;
if (cartridgeRom is not null)
{
    Console.WriteLine($"Cartridge loaded: {Path.GetFileName(options.CartPath)} ({cartridgeRom.Length} bytes)");
}

if (options.RamExpansion != RamExpansion.None)
{
    Console.WriteLine($"RAM Expansion: {options.RamExpansion}");
}

if (options.Gdb)
{
    var gdbMachine = new Vic20Machine(
        basicRom, kernalRom,
        charRom:      charRom,
        keyboard:     null,
        audio:        null,
        tape:         tape,
        ramExpansion: options.RamExpansion,
        cartridgeRom: cartridgeRom);

    gdbMachine.Reset();
    using var gdbTarget = new Cpu6502GdbTarget(gdbMachine.Cpu, gdbMachine.Bus);
    using var gdbServer = new RspServer(gdbTarget, options.GdbPort);

    Console.WriteLine($"[GDB] VIC-20 debug server listening on localhost:{options.GdbPort}");
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        gdbServer.Stop();
    };

    gdbServer.Start();
    while (gdbServer.IsRunning)
        Thread.Sleep(50);

    return 0;
}

// ── build machine and host ────────────────────────────────────────────────────
using var host = new RaylibHost(
    "Commodore VIC-20",
    new DisplayOptions(Scale: options.Scale, Smooth: options.Smooth, ScanlineIntensity: options.ScanlineIntensity),
    frameWidth: VicI.FrameWidth,
    frameHeight: VicI.FrameHeight,
    logKeypresses: options.DebugKeys);

var machine = new Vic20Machine(
    basicRom, kernalRom,
    charRom:      charRom,
    keyboard:     host,
    audio:        host,
    tape:         tape,
    ramExpansion: options.RamExpansion,
    cartridgeRom: cartridgeRom);

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
          --char       <path>   Character ROM image (4KB)
          --tape       <path>   TAP tape image
          --cart       <path>   ROM cartridge image (4KB/8KB/16KB)
          --ram        <size>   RAM expansion (3k, 8k, 16k, 24k, 32k)
          --scale      <n>      Window scale factor (default: 3)
          --smooth             Enable bilinear texture filtering (smooth scaling)
          --scanlines  <0..1>   CRT scanline intensity (0 = off, 0.5 = moderate, default 0)
          --debug-keys         Log raw keypresses from Raylib (debug only)
          --gdb                Run a headless GDB remote debugging server on localhost:1234
          --gdb-port   <n>     GDB server port (default: 1234)
        """);
}
