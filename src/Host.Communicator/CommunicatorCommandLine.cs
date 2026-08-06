namespace Host.Communicator;

public sealed record CommunicatorOptions(
    string RomPath,
    int Scale,
    bool Smooth,
    float Scanlines
);

public static class CommunicatorCommandLine
{
    public static CommunicatorOptions Parse(string[] args)
    {
        string romPath = "roms/communicator/os.rom";
        int scale = 3;
        bool smooth = false;
        float scanlines = 0f;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--rom":
                case "--os":
                    romPath = RequireValue(args, ref i, "--rom");
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

        return new CommunicatorOptions(romPath, scale, smooth, scanlines);
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {optionName}.");

        index++;
        return args[index];
    }
}
