namespace Host.Vic20;

public sealed record Vic20Options(
    string BasicPath,
    string KernalPath,
    string? CharPath,
    string? TapePath,
    int Scale,
    bool Smooth,
    float ScanlineIntensity,
    bool DebugKeys,
    bool Gdb,
    int GdbPort);

public static class Vic20CommandLine
{
    public static Vic20Options Parse(string[] args)
    {
        string? basicPath = null;
        string? kernalPath = null;
        string? charPath = null;
        string? tapePath = null;
        int scale = 3;
        bool smooth = false;
        float scanlineIntensity = 0f;
        bool debugKeys = false;
        bool gdb = false;
        int gdbPort = 1234;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--basic": basicPath = RequireValue(args, ref i, "--basic"); break;
                case "--kernal": kernalPath = RequireValue(args, ref i, "--kernal"); break;
                case "--char": charPath = RequireValue(args, ref i, "--char"); break;
                case "--tape": tapePath = RequireValue(args, ref i, "--tape"); break;
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
                case "--debug-keys":
                    debugKeys = true;
                    break;
                case "--gdb":
                    gdb = true;
                    break;
                case "--gdb-port":
                    if (!int.TryParse(RequireValue(args, ref i, "--gdb-port"), out gdbPort) || gdbPort is < 1 or > 65535)
                        throw new ArgumentException("Invalid value for --gdb-port (must be 1-65535).");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        if (basicPath is null || kernalPath is null)
            throw new ArgumentException("--basic and --kernal are required.");

        return new Vic20Options(basicPath, kernalPath, charPath, tapePath, scale, smooth, scanlineIntensity, debugKeys, gdb, gdbPort);
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {optionName}.");

        index++;
        return args[index];
    }
}

