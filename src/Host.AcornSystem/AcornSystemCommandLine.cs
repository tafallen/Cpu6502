using Machines.AcornSystem;

namespace Host.AcornSystem;

public sealed record AcornSystemOptions(
    string RomPath,
    string? DiscPath,
    AcornSystemModel Model,
    int Scale,
    bool Smooth,
    float ScanlineIntensity
);

public static class AcornSystemCommandLine
{
    public static AcornSystemOptions Parse(string[] args)
    {
        string romPath = "roms/acornsystem/sys3.rom";
        string? discPath = null;
        AcornSystemModel model = AcornSystemModel.System3;
        int scale = 3;
        bool smooth = false;
        float scanlineIntensity = 0f;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--rom":
                case "--sys":
                    romPath = RequireValue(args, ref i, "--rom");
                    break;
                case "--disc":
                case "--dsk":
                    discPath = RequireValue(args, ref i, "--disc");
                    break;
                case "--model":
                    string val = RequireValue(args, ref i, "--model").ToLowerInvariant();
                    model = val switch
                    {
                        "system1" or "1" => AcornSystemModel.System1,
                        "system2" or "2" => AcornSystemModel.System2,
                        "system3" or "3" => AcornSystemModel.System3,
                        "system4" or "4" => AcornSystemModel.System4,
                        "system5" or "5" => AcornSystemModel.System5,
                        _ => throw new ArgumentException($"Invalid model '{val}'. Valid options: system1, system2, system3, system4, system5.")
                    };
                    break;
                case "--scale":
                    if (!int.TryParse(RequireValue(args, ref i, "--scale"), out scale))
                        throw new ArgumentException("Invalid value for --scale.");
                    break;
                case "--smooth":
                    smooth = true;
                    break;
                case "--scanlines":
                    if (!float.TryParse(RequireValue(args, ref i, "--scanlines"), out scanlineIntensity) ||
                        scanlineIntensity < 0f || scanlineIntensity > 1f)
                        throw new ArgumentException("Invalid value for --scanlines (must be 0.0-1.0).");
                    break;
            }
        }

        return new AcornSystemOptions(romPath, discPath, model, scale, smooth, scanlineIntensity);
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {optionName}.");

        index++;
        return args[index];
    }
}
