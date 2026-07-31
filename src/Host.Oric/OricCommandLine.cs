namespace Host.Oric;

public sealed record OricOptions(
    string OsPath,
    string TapePath,
    int Scale,
    bool Smooth,
    float Scanlines
);

public static class OricCommandLine
{
    public static OricOptions Parse(string[] args)
    {
        string osPath = "roms/oric/atmos.rom";
        string tapePath = "";
        int scale = 3;
        bool smooth = false;
        float scanlines = 0f;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--os":
                case "--rom":
                    osPath = args[++i];
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

        return new OricOptions(osPath, tapePath, scale, smooth, scanlines);
    }
}
