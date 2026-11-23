using System.Text.Json.Serialization;
using FluxProHWiNFO.HWiNFO;

namespace FluxProHWiNFO.Config;

/// <summary>Settings stored in config.json.</summary>
public sealed class AppConfig
{
    /// <summary>The display goes dark about 2 s after the last write, so the send interval is capped at 1 s.</summary>
    public const int MaxIntervalMs = 1000;
    public const int MinIntervalMs = 250;

    public SensorConfig Cpu { get; set; } = SensorConfig.DefaultCpu();
    public SensorConfig Gpu { get; set; } = SensorConfig.DefaultGpu();

    /// <summary>How often to read HWiNFO and write the display, in ms. Clamped to 250–1000.</summary>
    public int PollIntervalMs { get; set; } = 1000;

    /// <summary>Show °F instead of °C. The display has two integer digits, so anything above 99.9 °F shows as 99.9.</summary>
    public bool Fahrenheit { get; set; }

    [JsonIgnore]
    public int EffectiveIntervalMs => Math.Clamp(PollIntervalMs, MinIntervalMs, MaxIntervalMs);

    /// <summary>Fills in anything a hand-edited file left null.</summary>
    public AppConfig Normalize()
    {
        Cpu ??= SensorConfig.DefaultCpu();
        Gpu ??= SensorConfig.DefaultGpu();
        Cpu.Labels ??= [];
        Gpu.Labels ??= [];
        return this;
    }
}

public sealed class SensorConfig
{
    /// <summary>HWiNFO labels to try, in priority order. An empty list leaves this value blank on the display.</summary>
    public List<string> Labels { get; set; } = [];

    /// <summary>Optional text the HWiNFO sensor name must contain, e.g. "RTX" to skip an integrated GPU.</summary>
    public string? SensorContains { get; set; }

    public SensorSelector ToSelector() => new(Labels ?? [], string.IsNullOrWhiteSpace(SensorContains) ? null : SensorContains);

    public static SensorConfig DefaultCpu() => new() { Labels = [.. SensorSelector.DefaultCpu.Labels] };
    public static SensorConfig DefaultGpu() => new() { Labels = [.. SensorSelector.DefaultGpu.Labels] };
}
