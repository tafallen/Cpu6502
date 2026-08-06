namespace Host.BbcMicro;

public sealed record BbcMicroOptions(
    string OsPath,
    string BasicPath,
    Machines.BbcMicro.BbcModel Model,
    bool Tube,
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
        Machines.BbcMicro.BbcModel model = Machines.BbcMicro.BbcModel.ModelB;
        bool tube = false;
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
                case "--tube":
                    tube = true;
                    break;
                case "--model":
                    string val = args[++i].ToLowerInvariant();
                    model = val switch
                    {
                        "a" or "modela" => Machines.BbcMicro.BbcModel.ModelA,
                        "b" or "modelb" => Machines.BbcMicro.BbcModel.ModelB,
                        "bplus64" or "b+" or "modelbplus64" => Machines.BbcMicro.BbcModel.ModelBPlus64,
                        _ => throw new ArgumentException($"Invalid model variant '{val}'. Options: a, b, b+.")
                    };
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

        return new BbcMicroOptions(osPath, basicPath, model, tube, scale, smooth, scanlines);
    }
}
