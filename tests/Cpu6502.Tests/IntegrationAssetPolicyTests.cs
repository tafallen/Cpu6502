namespace Cpu6502.Tests;

public class IntegrationAssetPolicyTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData(null, false)]
    public void IsTruthy_ParsesExpectedValues(string? value, bool expected)
    {
        Assert.Equal(expected, IntegrationAssetPolicy.IsTruthy(value));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    public void ShouldRun_RespectsAssetAndStrictMode(bool assetExists, bool strictMode, bool expected)
    {
        Assert.Equal(expected, IntegrationAssetPolicy.ShouldRun(assetExists, strictMode));
    }

    [Fact]
    public void BuildMissingAssetMessage_IncludesPathAndEnvVar()
    {
        string message = IntegrationAssetPolicy.BuildMissingAssetMessage(
            "TestData/6502_functional_test.bin",
            "Download from Klaus2m5.");

        Assert.Contains("TestData/6502_functional_test.bin", message);
        Assert.Contains(IntegrationAssetPolicy.RequireAssetsEnvVar, message);
        Assert.Contains("Download from Klaus2m5.", message);
    }

    [Fact]
    public void ResolveAssetPath_ReturnsExistingCandidate()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string existing = Path.Combine(tempDir, "asset.bin");
            File.WriteAllBytes(existing, [0x42]);

            string resolved = IntegrationAssetPolicy.ResolveAssetPath(
                Path.Combine(tempDir, "missing.bin"),
                existing);

            Assert.Equal(existing, resolved);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveAssetPathByFileName_UsesConfiguredAssetPath()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string previous = Environment.GetEnvironmentVariable(IntegrationAssetPolicy.AssetPathEnvVar) ?? string.Empty;

        try
        {
            string assetPath = Path.Combine(tempDir, "6502_functional_test.bin");
            File.WriteAllBytes(assetPath, [0x00, 0x01]);
            Environment.SetEnvironmentVariable(IntegrationAssetPolicy.AssetPathEnvVar, assetPath);

            string resolved = IntegrationAssetPolicy.ResolveAssetPathByFileName("6502_functional_test.bin");
            Assert.Equal(assetPath, resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                IntegrationAssetPolicy.AssetPathEnvVar,
                string.IsNullOrEmpty(previous) ? null : previous);
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
