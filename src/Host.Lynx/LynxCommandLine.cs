namespace Host.Lynx;

public sealed record LynxOptions(
    string? CartPath,
    string? BootPath,
    int Scale,
    bool Smooth,
    float Scanlines
);

public static class LynxCommandLine
{
    public static LynxOptions Parse(string[] args)
    {
        string? cartPath = null;
        string? bootPath = null;
        int scale = 4;
        bool smooth = false;
        float scanlines = 0f;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--cart":
                case "--lnx":
                    cartPath = RequireValue(args, ref i, "--cart");
                    break;
                case "--boot":
                case "--rom":
                    bootPath = RequireValue(args, ref i, "--boot");
                    break;
                case "--scale":
                    if (!int.TryParse(RequireValue(args, ref i, "--scale"), out scale))
                        throw new ArgumentException("Invalid value for --scale.");
                    break;
                case "--smooth":
                    smooth = true;
                    break;
                case "--scanlines":
                    if (!float.TryParse(RequireValue(args, ref i, "--scanlines"), out scanlines) ||
                        scanlines < 0f || scanlines > 1f)
                        throw new ArgumentException("Invalid value for --scanlines.");
                    break;
            }
        }

        return new LynxOptions(cartPath, bootPath, scale, smooth, scanlines);
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {optionName}.");

        index++;
        return args[index];
    }
}
