using Machines.Plus4;

namespace Host.Plus4;

public sealed record Plus4Options(
    string KernalPath,
    string BasicPath,
    string? PrgPath,
    Plus4Model Model,
    int Scale,
    bool Smooth,
    float ScanlineIntensity
);

public static class Plus4CommandLine
{
    public static Plus4Options Parse(string[] args)
    {
        string kernalPath = "roms/plus4/kernal.bin";
        string basicPath = "roms/plus4/basic.bin";
        string? prgPath = null;
        Plus4Model model = Plus4Model.Plus4;
        int scale = 3;
        bool smooth = false;
        float scanlineIntensity = 0f;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--kernal":
                    kernalPath = RequireValue(args, ref i, "--kernal");
                    break;
                case "--basic":
                    basicPath = RequireValue(args, ref i, "--basic");
                    break;
                case "--prg":
                    prgPath = RequireValue(args, ref i, "--prg");
                    break;
                case "--model":
                    string val = RequireValue(args, ref i, "--model").ToLowerInvariant();
                    model = val switch
                    {
                        "c16" or "16" => Plus4Model.C16,
                        "plus4" or "4" => Plus4Model.Plus4,
                        _ => throw new ArgumentException($"Invalid model '{val}'. Valid options: plus4, c16.")
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

        return new Plus4Options(kernalPath, basicPath, prgPath, model, scale, smooth, scanlineIntensity);
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {optionName}.");

        index++;
        return args[index];
    }
}
