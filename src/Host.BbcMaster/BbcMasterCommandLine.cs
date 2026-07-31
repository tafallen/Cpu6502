namespace Host.BbcMaster;

public sealed record BbcMasterOptions(
    string OsPath,
    string DiscPath,
    int Scale,
    bool Smooth,
    float Scanlines
);

public static class BbcMasterCommandLine
{
    public static BbcMasterOptions Parse(string[] args)
    {
        string osPath = "roms/bbcmaster/mos320.rom";
        string discPath = "";
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
                case "--disc":
                case "--adf":
                    discPath = args[++i];
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

        return new BbcMasterOptions(osPath, discPath, scale, smooth, scanlines);
    }
}
