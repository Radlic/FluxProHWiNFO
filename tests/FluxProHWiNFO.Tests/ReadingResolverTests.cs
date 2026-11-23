using FluxProHWiNFO.Config;
using FluxProHWiNFO.Core;
using FluxProHWiNFO.HWiNFO;

namespace FluxProHWiNFO.Tests;

public class ReadingResolverTests
{
    private static VsbEntry Entry(int i, string sensor, string label, string raw, string unit = "°C") =>
        new(i, sensor, label, $"{raw} {unit}", raw, "");

    private static readonly VsbEntry Cpu = Entry(0, "CPU [#0]: AMD Ryzen 7 9800X3D: Enhanced", "CPU (Tctl/Tdie)", "56.4");
    private static readonly VsbEntry Igpu = Entry(1, "GPU [#1]: AMD Radeon(TM) Graphics", "GPU Temperature", "38.0");
    private static readonly VsbEntry Rtx = Entry(2, "GPU [#0]: NVIDIA GeForce RTX 5070 Ti", "GPU Temperature", "44.5");

    private static VsbSnapshot Snapshot(params VsbEntry[] entries) => new(true, entries);

    [Fact]
    public void Ok_MatchesBothValues()
    {
        var r = ReadingResolver.Resolve(Snapshot(Cpu, Rtx), hwinfoRunning: true, new AppConfig());
        Assert.Equal(HwinfoStatus.Ok, r.Status);
        Assert.Equal(56.4, r.CpuDisplay);
        Assert.Equal(44.5, r.GpuDisplay);
        Assert.Same(Rtx, r.Gpu!.Entry);
    }

    [Fact]
    public void HwinfoNotRunning_BlanksEvenIfRegistryHasValues()
    {
        var r = ReadingResolver.Resolve(Snapshot(Cpu, Rtx), hwinfoRunning: false, new AppConfig());
        Assert.Equal(HwinfoStatus.NotRunning, r.Status);
        Assert.Null(r.CpuDisplay);
        Assert.Null(r.GpuDisplay);
    }

    [Fact]
    public void KeyMissing_Blanks()
    {
        var r = ReadingResolver.Resolve(VsbSnapshot.NotFound, hwinfoRunning: true, new AppConfig());
        Assert.Equal(HwinfoStatus.KeyMissing, r.Status);
        Assert.Null(r.CpuDisplay);
    }

    [Fact]
    public void ReadError_Blanks()
    {
        var r = ReadingResolver.Resolve(null, hwinfoRunning: true, new AppConfig());
        Assert.Equal(HwinfoStatus.ReadError, r.Status);
    }

    [Fact]
    public void MissingGpu_OnlyGpuBlank()
    {
        var r = ReadingResolver.Resolve(Snapshot(Cpu), hwinfoRunning: true, new AppConfig());
        Assert.Equal(56.4, r.CpuDisplay);
        Assert.Null(r.GpuDisplay);
        Assert.Null(r.Gpu);
    }

    [Fact]
    public void SensorContains_PicksRtxOverIntegratedGpu()
    {
        var config = new AppConfig { Gpu = new SensorConfig { Labels = ["GPU Temperature"], SensorContains = "RTX" } };
        var r = ReadingResolver.Resolve(Snapshot(Cpu, Igpu, Rtx), hwinfoRunning: true, config);
        Assert.Equal(44.5, r.GpuDisplay);
    }

    [Fact]
    public void EmptyLabels_LeavesValueBlank()
    {
        var config = new AppConfig { Gpu = new SensorConfig { Labels = [] } };
        var r = ReadingResolver.Resolve(Snapshot(Cpu, Rtx), hwinfoRunning: true, config);
        Assert.Null(r.GpuDisplay);
    }

    [Fact]
    public void Fahrenheit_ConvertsCelsiusInput()
    {
        var config = new AppConfig { Fahrenheit = true };
        var r = ReadingResolver.Resolve(Snapshot(Entry(0, "CPU", "CPU (Tctl/Tdie)", "30.0")), hwinfoRunning: true, config);
        Assert.Equal(86.0, r.CpuDisplay!.Value, precision: 6);
    }

    [Fact]
    public void HwinfoInFahrenheit_IsConvertedBackToCelsius()
    {
        var f = Entry(0, "CPU", "CPU (Tctl/Tdie)", "122.0", unit: "°F");
        var r = ReadingResolver.Resolve(Snapshot(f), hwinfoRunning: true, new AppConfig());
        Assert.Equal(50.0, r.CpuDisplay!.Value, precision: 6);
    }

    [Fact]
    public void HwinfoInFahrenheit_ShownInFahrenheitUnchanged()
    {
        var f = Entry(0, "CPU", "CPU (Tctl/Tdie)", "95.0", unit: "°F");
        var r = ReadingResolver.Resolve(Snapshot(f), hwinfoRunning: true, new AppConfig { Fahrenheit = true });
        Assert.Equal(95.0, r.CpuDisplay!.Value, precision: 6);
    }

    [Fact]
    public void Readings_HaveValueEquality()
    {
        var a = ReadingResolver.Resolve(Snapshot(Cpu, Rtx), true, new AppConfig());
        var b = ReadingResolver.Resolve(Snapshot(Cpu, Rtx), true, new AppConfig());
        Assert.Equal(a, b);
    }
}
