namespace Host.BbcMicro;

public sealed record BbcMicroOptions(
    string OsPath,
    string BasicPath,
    int Scale,
    bool Smooth,
    float Scanlines
);

public static class BbcMicroCommandLine
{
    public static BbcMicroOptions Parse(string[] args)
    {
        string osPath = "roms/bbcmicro/os12.rom";
        string basicPath = "roms/bbcmicro/basic2.rom";
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

        return new BbcMicroOptions(osPath, basicPath, scale, smooth, scanlines);
    }
}
