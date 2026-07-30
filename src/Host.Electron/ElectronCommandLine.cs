namespace Host.Electron;

public sealed record ElectronOptions(
    string OsPath,
    string BasicPath,
    string? TapePath,
    int Scale,
    bool Smooth,
    float Scanlines
);

public static class ElectronCommandLine
{
    public static ElectronOptions Parse(string[] args)
    {
        string osPath = "roms/electron/os.rom";
        string basicPath = "roms/electron/basic.rom";
        string? tapePath = null;
        int scale = 3;
        bool smooth = false;
        float scanlines = 0f;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--os":
                    osPath = args[++i];
                    break;
                case "--basic":
                    basicPath = args[++i];
                    break;
                case "--tape":
                    tapePath = args[++i];
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

        return new ElectronOptions(osPath, basicPath, tapePath, scale, smooth, scanlines);
    }
}
