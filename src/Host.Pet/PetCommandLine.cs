namespace Host.Pet;

public sealed record PetOptions(
    string RomPath,
    string PrgPath,
    int Scale,
    bool Smooth,
    float Scanlines
);

public static class PetCommandLine
{
    public static PetOptions Parse(string[] args)
    {
        string romPath = "roms/pet/pet2001.rom";
        string prgPath = "";
        int scale = 3;
        bool smooth = false;
        float scanlines = 0f;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--rom":
                case "--kernel":
                    romPath = args[++i];
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

        return new PetOptions(romPath, prgPath, scale, smooth, scanlines);
    }
}
