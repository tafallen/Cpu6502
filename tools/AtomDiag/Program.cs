using Machines.Atom;

string root   = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
string romDir = Path.GetFullPath(Path.Combine(root, "roms", "Acorn Atom"));
string cache  = Path.GetFullPath(Path.Combine(root, "tools", "AtomDiag", "rom_cache"));
Directory.CreateDirectory(cache);

// ── ROM loading with byte-cache ──────────────────────────────────────────────
byte[] LoadRom(string name)
{
    string src   = Path.Combine(romDir, name);
    string dump  = Path.Combine(cache, Path.ChangeExtension(name, ".hex"));
    byte[] bytes = File.ReadAllBytes(src);
    string sig   = $"{bytes.Length}:{bytes[0]:X2}:{bytes[^1]:X2}";
    string sigFile = dump + ".sig";
    if (!File.Exists(dump) || !File.Exists(sigFile) || File.ReadAllText(sigFile) != sig)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < bytes.Length; i += 16)
        {
            sb.Append($"  {i:X4}: ");
            for (int j = 0; j < 16 && i+j < bytes.Length; j++)
                sb.Append($"{bytes[i+j]:X2} ");
            sb.AppendLine();
        }
        File.WriteAllText(dump, sb.ToString());
        File.WriteAllText(sigFile, sig);
        Console.WriteLine($"[cache] wrote {Path.GetFileName(dump)}");
    }
    return bytes;
}

// Correct Atomulator mapping: basic→$C000, float→$D000, dos→$E000, kernel→$F000
byte[] basicRom = LoadRom("abasic.rom");   // $C000-$CFFF
byte[] osRom    = LoadRom("akernel.rom");  // $F000-$FFFF
byte[] floatRom = LoadRom("afloat.rom");   // $D000-$DFFF
byte[] dosRom   = LoadRom("dosrom.rom");   // $E000-$EFFF
Console.WriteLine($"BASIC: {basicRom.Length}b  Float: {floatRom.Length}b  DOS: {dosRom.Length}b  OS: {osRom.Length}b");

var machine = new AtomMachine(basicRom, osRom, floatRom: floatRom, dosRom: dosRom);
machine.Reset();
Console.WriteLine($"PC after reset: ${machine.Cpu.PC:X4}");

// ── Run 200 frames with hot-spot profiling ────────────────────────────────────
var pcHits = new Dictionary<ushort, int>();
for (int frame = 0; frame < 200; frame++)
{
    machine.Cpu.Irq();
    ulong target = machine.Cpu.TotalCycles + 20_000;
    while (machine.Cpu.TotalCycles < target)
    {
        ushort pc = machine.Cpu.PC;
        pcHits.TryGetValue(pc, out int n);
        pcHits[pc] = n + 1;
        machine.Step();
    }
}
Console.WriteLine($"Final PC=${machine.Cpu.PC:X4}, unique PCs={pcHits.Count}");
Console.WriteLine("Top 20 hot spots:");
foreach (var kv in pcHits.OrderByDescending(x => x.Value).Take(20))
{
    byte op = machine.Bus.Read(kv.Key);
    byte b1 = machine.Bus.Read((ushort)(kv.Key + 1));
    Console.WriteLine($"  ${kv.Key:X4}: {op:X2} {b1:X2}  hits={kv.Value}");
}

// ── Screen dump ───────────────────────────────────────────────────────────────
Console.WriteLine("--- Screen (32x16) ---");
for (int row = 0; row < 16; row++)
{
    var sb = new System.Text.StringBuilder("|");
    for (int col = 0; col < 32; col++)
    {
        byte b    = machine.VideoRam.Read((ushort)(row * 32 + col));
        bool inv  = (b & 0x80) != 0;
        byte code = (byte)(b & 0x3F);
        char c;
        if (code == 0) c = '@';
        else if (code <= 0x1A) c = (char)('A' + code - 1);
        else if (code == 0x1B) c = '[';
        else if (code == 0x1C) c = '\\';
        else if (code == 0x1D) c = ']';
        else if (code == 0x1E) c = '^';
        else if (code == 0x1F) c = '_';
        else c = (char)(code);
        if (inv) c = char.ToLower(c);
        sb.Append(c);
    }
    sb.Append('|');
    Console.WriteLine(sb);
}
