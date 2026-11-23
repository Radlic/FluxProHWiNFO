using FluxProHWiNFO.Config;

namespace FluxProHWiNFO.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FluxProHWiNFO.Tests", Guid.NewGuid().ToString("N"));

    public ConfigStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string PathOf(string name = "config.json") => Path.Combine(_dir, name);

    [Fact]
    public void MissingFile_IsCreatedWithDefaults()
    {
        string path = Path.Combine(_dir, "sub", "config.json");
        var (config, error) = ConfigStore.LoadOrCreate(path);

        Assert.Null(error);
        Assert.NotNull(config);
        Assert.True(File.Exists(path));
        Assert.Equal(["CPU (Tctl/Tdie)", "CPU Die (average)"], config.Cpu.Labels);
        Assert.Equal(["GPU Temperature"], config.Gpu.Labels);
        Assert.Equal(1000, config.PollIntervalMs);
        Assert.False(config.Fahrenheit);
    }

    [Fact]
    public void CreatedFile_RoundTrips()
    {
        ConfigStore.LoadOrCreate(PathOf());
        var (config, error) = ConfigStore.LoadOrCreate(PathOf());
        Assert.Null(error);
        Assert.Equal(["GPU Temperature"], config!.Gpu.Labels);
    }

    [Fact]
    public void CreatedFile_UsesCamelCase()
    {
        ConfigStore.LoadOrCreate(PathOf());
        string json = File.ReadAllText(PathOf());
        Assert.Contains("\"pollIntervalMs\"", json);
        Assert.Contains("\"sensorContains\"", json);
        Assert.DoesNotContain("effectiveIntervalMs", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HandEditedFile_WithCommentsAndTrailingCommas_Loads()
    {
        File.WriteAllText(PathOf(), """
            {
              // use the hot spot instead
              "gpu": { "labels": ["GPU Hot Spot Temperature"], "sensorContains": "RTX", },
              "fahrenheit": true,
            }
            """);

        var (config, error) = ConfigStore.LoadOrCreate(PathOf());

        Assert.Null(error);
        Assert.Equal(["GPU Hot Spot Temperature"], config!.Gpu.Labels);
        Assert.Equal("RTX", config.Gpu.SensorContains);
        Assert.True(config.Fahrenheit);
        Assert.Equal(["CPU (Tctl/Tdie)", "CPU Die (average)"], config.Cpu.Labels); // untouched default
    }

    [Fact]
    public void NullSections_FallBackToDefaults()
    {
        File.WriteAllText(PathOf(), """{ "cpu": null, "gpu": { "labels": null } }""");
        var (config, _) = ConfigStore.LoadOrCreate(PathOf());
        Assert.NotEmpty(config!.Cpu.Labels);
        Assert.Empty(config.Gpu.Labels);
    }

    [Fact]
    public void InvalidJson_ReturnsErrorAndLeavesFileAlone()
    {
        File.WriteAllText(PathOf(), "{ not json");
        var (config, error) = ConfigStore.LoadOrCreate(PathOf());

        Assert.Null(config);
        Assert.StartsWith("invalid JSON", error);
        Assert.Equal("{ not json", File.ReadAllText(PathOf()));
    }

    [Fact]
    public void EmptyFile_GivesDefaults()
    {
        File.WriteAllText(PathOf(), "");
        var (config, error) = ConfigStore.LoadOrCreate(PathOf());
        Assert.Null(error);
        Assert.NotNull(config);
    }

    [Theory]
    [InlineData(1000, 1000)]
    [InlineData(500, 500)]
    [InlineData(2000, 1000)] // display goes dark after ~2 s without data
    [InlineData(50, 250)]
    [InlineData(-1, 250)]
    public void PollInterval_IsClamped(int configured, int effective)
    {
        Assert.Equal(effective, new AppConfig { PollIntervalMs = configured }.EffectiveIntervalMs);
    }

    [Fact]
    public void ResolvePath_PrefersConfigNextToExe()
    {
        string exeDir = Path.Combine(_dir, "exe");
        string appData = Path.Combine(_dir, "appdata");
        Directory.CreateDirectory(exeDir);

        Assert.Equal(Path.Combine(appData, "config.json"), ConfigStore.ResolvePath(exeDir, appData));

        File.WriteAllText(Path.Combine(exeDir, "config.json"), "{}");
        Assert.Equal(Path.Combine(exeDir, "config.json"), ConfigStore.ResolvePath(exeDir, appData));
    }

    [Fact]
    public void SensorConfig_BlankSensorContainsIsIgnored()
    {
        var selector = new SensorConfig { Labels = ["X"], SensorContains = "  " }.ToSelector();
        Assert.Null(selector.SensorContains);
    }
}
