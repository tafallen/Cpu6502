namespace Host.Atari800;

public sealed record AtariCommandLineOptions(
    string? OsRomPath = null,
    string? BasicRomPath = null,
    string? XexPath = null,
    string? AtrPath = null,
    int Scale = 3,
    bool Smooth = false,
    float Scanlines = 0f
);

public static class AtariCommandLine
{
    public static AtariCommandLineOptions Parse(string[] args)
    {
        string? osRomPath = null;
        string? basicRomPath = null;
        string? xexPath = null;
        string? atrPath = null;
        int scale = 3;
        bool smooth = false;
        float scanlines = 0f;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--os":
                    if (i + 1 < args.Length) osRomPath = args[++i];
                    break;
                case "--basic":
                    if (i + 1 < args.Length) basicRomPath = args[++i];
                    break;
                case "--xex":
                    if (i + 1 < args.Length) xexPath = args[++i];
                    break;
                case "--atr":
                    if (i + 1 < args.Length) atrPath = args[++i];
                    break;
                case "--scale":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int s)) scale = s;
                    break;
                case "--smooth":
                    smooth = true;
                    break;
                case "--scanlines":
                    if (i + 1 < args.Length && float.TryParse(args[++i], out float sc)) scanlines = sc;
                    break;
            }
        }

        return new AtariCommandLineOptions(osRomPath, basicRomPath, xexPath, atrPath, scale, smooth, scanlines);
    }
}
