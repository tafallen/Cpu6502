namespace Host.C64;

public sealed record C64Options(
    string KernalPath,
    string BasicPath,
    string CharPath,
    string PrgPath,
    int Scale,
    bool Smooth,
    float Scanlines
);

public static class C64CommandLine
{
    public static C64Options Parse(string[] args)
    {
        string kernalPath = "roms/c64/kernal.rom";
        string basicPath  = "roms/c64/basic.rom";
        string charPath   = "roms/c64/chargen.rom";
        string prgPath    = "";
        int scale = 3;
        bool smooth = false;
        float scanlines = 0f;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--kernal":
                    kernalPath = args[++i];
                    break;
                case "--basic":
                    basicPath = args[++i];
                    break;
                case "--char":
                    charPath = args[++i];
                    break;
                case "--prg":
                    prgPath = args[++i];
                    break;
                case "--scale":
                    scale = int.Parse(args[++i]);
                    break;
                case "--smooth":
                    smooth = true;
                    break;
                case "--scanlines":
                    scanlines = float.Parse(args[++i]);
                    break;
            }
        }

        return new C64Options(kernalPath, basicPath, charPath, prgPath, scale, smooth, scanlines);
    }
}
