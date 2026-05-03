using Xunit;

namespace Cpu6502.Tests;

internal static class IntegrationAssetPolicy
{
    internal const string RequireAssetsEnvVar = "CPU6502_REQUIRE_INTEGRATION_ASSETS";
    internal const string AssetPathEnvVar = "CPU6502_INTEGRATION_ASSET_PATH";

    internal static bool IsStrictModeEnabled()
    {
        return IsTruthy(Environment.GetEnvironmentVariable(RequireAssetsEnvVar));
    }

    internal static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "1" => true,
            "true" => true,
            "yes" => true,
            "on" => true,
            _ => false
        };
    }

    internal static bool ShouldRun(bool assetExists, bool strictModeEnabled)
    {
        return assetExists || !strictModeEnabled;
    }

    internal static string ResolveAssetPath(params string[] candidatePaths)
    {
        foreach (string path in candidatePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            if (File.Exists(path))
                return path;
        }

        return candidatePaths[0];
    }

    internal static string ResolveAssetPathByFileName(string fileName)
    {
        string? configuredPath = Environment.GetEnvironmentVariable(AssetPathEnvVar);
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return ResolveAssetPath(configuredPath);

        string cwd = Directory.GetCurrentDirectory();
        string appBase = AppContext.BaseDirectory;

        return ResolveAssetPath(
            Path.Combine("TestData", fileName),
            Path.Combine("tests", "Cpu6502.Tests", "TestData", fileName),
            Path.Combine(cwd, "TestData", fileName),
            Path.Combine(cwd, "tests", "Cpu6502.Tests", "TestData", fileName),
            Path.Combine(appBase, "TestData", fileName),
            Path.GetFullPath(Path.Combine(appBase, "..", "..", "..", "..", "TestData", fileName)),
            FindInParents(cwd, fileName),
            FindInParents(appBase, fileName));
    }

    private static string? FindInParents(string startPath, string fileName)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir is not null)
        {
            string underTestData = Path.Combine(dir.FullName, "TestData", fileName);
            if (File.Exists(underTestData))
                return underTestData;

            string underProjectData = Path.Combine(dir.FullName, "tests", "Cpu6502.Tests", "TestData", fileName);
            if (File.Exists(underProjectData))
                return underProjectData;

            dir = dir.Parent;
        }

        return null;
    }

    internal static string BuildMissingAssetMessage(string assetPath, string acquisitionHint)
    {
        return
            $"Required integration asset is missing: '{assetPath}'. " +
            $"Set {RequireAssetsEnvVar}=0 (or unset) for permissive local runs, " +
            $"or add the required asset. You can also point directly to the file with {AssetPathEnvVar}. {acquisitionHint}";
    }

    internal static bool ShouldRunAssetBackedTest(string assetPath, string acquisitionHint)
    {
        bool assetExists = File.Exists(assetPath);
        bool strictModeEnabled = IsStrictModeEnabled();

        if (!ShouldRun(assetExists, strictModeEnabled))
            Assert.Fail(BuildMissingAssetMessage(assetPath, acquisitionHint));

        return assetExists;
    }
}
