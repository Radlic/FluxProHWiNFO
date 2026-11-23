namespace FluxProHWiNFO.HWiNFO;

/// <summary>
/// Picks one VSB entry by label rather than by index, because HWiNFO renumbers entries whenever
/// the user changes which sensors are reported.
/// </summary>
/// <param name="Labels">Labels to try, in priority order (first one that has a numeric value wins). Case-insensitive exact match.</param>
/// <param name="SensorContains">Optional case-insensitive substring the entry's Sensor name must contain,
/// e.g. "RTX" to skip an integrated GPU that reports the same label.</param>
public sealed record SensorSelector(IReadOnlyList<string> Labels, string? SensorContains = null)
{
    public static SensorSelector DefaultCpu { get; } = new(["CPU (Tctl/Tdie)", "CPU Die (average)"]);
    public static SensorSelector DefaultGpu { get; } = new(["GPU Temperature"]);

    public SensorMatch? FindIn(IReadOnlyList<VsbEntry> entries)
    {
        foreach (string wanted in Labels)
        {
            string label = wanted.Trim();
            if (label.Length == 0)
                continue;

            foreach (var e in entries)
            {
                if (!string.Equals(e.Label?.Trim(), label, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrWhiteSpace(SensorContains)
                    && (e.Sensor is null || !e.Sensor.Contains(SensorContains.Trim(), StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (e.NumericValue is double v)
                    return new SensorMatch(e, v);
            }
        }
        return null;
    }

    public override string ToString()
    {
        string labels = string.Join(" | ", Labels.Select(l => $"\"{l}\""));
        return string.IsNullOrWhiteSpace(SensorContains) ? labels : $"{labels} (sensor contains \"{SensorContains}\")";
    }
}

public sealed record SensorMatch(VsbEntry Entry, double Value);
