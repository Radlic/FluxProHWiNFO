using System.Globalization;
using System.Text.RegularExpressions;

namespace FluxProHWiNFO.HWiNFO;

/// <summary>Turns the flat list of VSB registry values (Sensor0, Label0, ValueRaw0, ...) into entries.</summary>
public static partial class VsbParser
{
    [GeneratedRegex(@"^(Sensor|Label|ValueRaw|Value|Color)(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueNameRegex();

    public static IReadOnlyList<VsbEntry> Parse(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var byIndex = new SortedDictionary<int, Dictionary<string, string?>>();

        foreach (var (name, value) in values)
        {
            var m = ValueNameRegex().Match(name);
            if (!m.Success || !int.TryParse(m.Groups[2].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                continue;

            if (!byIndex.TryGetValue(index, out var fields))
                byIndex[index] = fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            fields[m.Groups[1].Value] = value;
        }

        return byIndex
            .Select(kv => new VsbEntry(
                kv.Key,
                kv.Value.GetValueOrDefault("Sensor"),
                kv.Value.GetValueOrDefault("Label"),
                kv.Value.GetValueOrDefault("Value"),
                kv.Value.GetValueOrDefault("ValueRaw"),
                kv.Value.GetValueOrDefault("Color")))
            .ToList();
    }

    /// <summary>
    /// Parses a ValueRaw string such as "54.3". Accepts "," as the decimal separator too.
    /// Returns null for empty, non-numeric or non-finite text.
    /// </summary>
    public static double? ParseRaw(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        string s = raw.Trim();
        if (s.Contains(',') && !s.Contains('.'))
            s = s.Replace(',', '.');

        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v)
            ? v
            : null;
    }
}
