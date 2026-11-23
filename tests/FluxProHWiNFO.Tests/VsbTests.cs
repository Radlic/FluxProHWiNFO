using FluxProHWiNFO.HWiNFO;

namespace FluxProHWiNFO.Tests;

public class VsbTests
{
    private sealed class FakeSource(IReadOnlyList<KeyValuePair<string, string?>>? values) : IVsbSource
    {
        public IReadOnlyList<KeyValuePair<string, string?>>? ReadValues() => values;
    }

    private static List<KeyValuePair<string, string?>> Entry(int i, string sensor, string label, string raw, string? unit = "°C") =>
    [
        new($"Sensor{i}", sensor),
        new($"Label{i}", label),
        new($"Value{i}", unit is null ? raw : $"{raw} {unit}"),
        new($"ValueRaw{i}", raw),
        new($"Color{i}", "FF0000"),
    ];

    private static IReadOnlyList<VsbEntry> Parse(params List<KeyValuePair<string, string?>>[] entries) =>
        VsbParser.Parse(entries.SelectMany(e => e));

    [Fact]
    public void Parse_GroupsValuesByIndex()
    {
        var entries = Parse(
            Entry(0, "CPU [#0]: AMD Ryzen 7 9800X3D: Enhanced", "CPU (Tctl/Tdie)", "54.3"),
            Entry(1, "GPU [#0]: NVIDIA GeForce RTX 5070 Ti", "GPU Temperature", "41.0"));

        Assert.Equal(2, entries.Count);
        var cpu = entries[0];
        Assert.Equal(0, cpu.Index);
        Assert.Equal("CPU [#0]: AMD Ryzen 7 9800X3D: Enhanced", cpu.Sensor);
        Assert.Equal("CPU (Tctl/Tdie)", cpu.Label);
        Assert.Equal("54.3 °C", cpu.Value);
        Assert.Equal("54.3", cpu.ValueRaw);
        Assert.Equal("FF0000", cpu.Color);
        Assert.Equal(54.3, cpu.NumericValue);
    }

    [Fact]
    public void Parse_DistinguishesValueFromValueRaw()
    {
        var entries = VsbParser.Parse([new("Value3", "12,5 °C"), new("ValueRaw3", "12.5")]);
        Assert.Equal("12,5 °C", entries[0].Value);
        Assert.Equal("12.5", entries[0].ValueRaw);
    }

    [Fact]
    public void Parse_SortsByIndexAndHandlesGaps()
    {
        var entries = Parse(Entry(12, "s", "b", "2"), Entry(3, "s", "a", "1"));
        Assert.Equal([3, 12], entries.Select(e => e.Index));
    }

    [Fact]
    public void Parse_IgnoresUnrelatedValueNames()
    {
        var entries = VsbParser.Parse([new("SomethingElse", "x"), new("Label", "no index"), new("Label1x", "bad")]);
        Assert.Empty(entries);
    }

    [Theory]
    [InlineData("54.3", 54.3)]
    [InlineData("54,3", 54.3)]
    [InlineData(" 54.3 ", 54.3)]
    [InlineData("-3.5", -3.5)]
    [InlineData("100", 100.0)]
    public void ParseRaw_AcceptsDotAndComma(string raw, double expected)
    {
        Assert.Equal(expected, VsbParser.ParseRaw(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Yes")]
    [InlineData("NaN")]
    public void ParseRaw_RejectsNonNumeric(string? raw)
    {
        Assert.Null(VsbParser.ParseRaw(raw));
    }

    [Fact]
    public void ParseRaw_IgnoresCurrentCulture()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal(54.3, VsbParser.ParseRaw("54.3"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    [Fact]
    public void Selector_MatchesByLabelNotIndex()
    {
        var entries = Parse(
            Entry(0, "Drive: Samsung", "Drive Temperature", "35"),
            Entry(1, "GPU [#0]: NVIDIA GeForce RTX 5070 Ti", "GPU Temperature", "41.0"),
            Entry(2, "CPU [#0]: AMD Ryzen 7 9800X3D: Enhanced", "CPU (Tctl/Tdie)", "54.3"));

        Assert.Equal(54.3, SensorSelector.DefaultCpu.FindIn(entries)?.Value);
        Assert.Equal(41.0, SensorSelector.DefaultGpu.FindIn(entries)?.Value);
    }

    [Fact]
    public void Selector_UsesFallbackLabelWhenPrimaryMissing()
    {
        var entries = Parse(Entry(0, "CPU [#0]: AMD Ryzen 7 9800X3D: Enhanced", "CPU Die (average)", "50.1"));
        var match = SensorSelector.DefaultCpu.FindIn(entries);
        Assert.NotNull(match);
        Assert.Equal("CPU Die (average)", match.Entry.Label);
    }

    [Fact]
    public void Selector_PrefersFirstLabelEvenIfFallbackHasLowerIndex()
    {
        var entries = Parse(
            Entry(0, "CPU", "CPU Die (average)", "50.1"),
            Entry(5, "CPU", "CPU (Tctl/Tdie)", "54.3"));
        Assert.Equal(54.3, SensorSelector.DefaultCpu.FindIn(entries)?.Value);
    }

    [Fact]
    public void Selector_LabelMatchIsCaseInsensitiveAndTrimmed()
    {
        var entries = Parse(Entry(0, "GPU", "  gpu temperature ", "41"));
        Assert.Equal(41.0, SensorSelector.DefaultGpu.FindIn(entries)?.Value);
    }

    [Fact]
    public void Selector_DoesNotMatchPartialLabel()
    {
        var entries = Parse(Entry(0, "GPU", "GPU Temperature Limit", "88"));
        Assert.Null(SensorSelector.DefaultGpu.FindIn(entries));
    }

    [Fact]
    public void Selector_SensorFilterSkipsOtherDevices()
    {
        var entries = Parse(
            Entry(0, "GPU [#1]: AMD Radeon(TM) Graphics", "GPU Temperature", "38"),
            Entry(1, "GPU [#0]: NVIDIA GeForce RTX 5070 Ti", "GPU Temperature", "41"));

        Assert.Equal(38.0, SensorSelector.DefaultGpu.FindIn(entries)?.Value);
        Assert.Equal(41.0, (SensorSelector.DefaultGpu with { SensorContains = "rtx" }).FindIn(entries)?.Value);
    }

    [Fact]
    public void Selector_SkipsNonNumericValue()
    {
        var entries = Parse(Entry(0, "CPU", "CPU (Tctl/Tdie)", "", unit: null), Entry(1, "CPU", "CPU Die (average)", "49"));
        Assert.Equal(49.0, SensorSelector.DefaultCpu.FindIn(entries)?.Value);
    }

    [Fact]
    public void Selector_ReturnsNullWhenAbsent()
    {
        Assert.Null(SensorSelector.DefaultCpu.FindIn([]));
    }

    [Fact]
    public void Reader_ReportsMissingKey()
    {
        var snapshot = new VsbReader(new FakeSource(null)).Read();
        Assert.False(snapshot.KeyFound);
        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public void Reader_ParsesSourceValues()
    {
        var snapshot = new VsbReader(new FakeSource(Entry(4, "GPU", "GPU Temperature", "40.5"))).Read();
        Assert.True(snapshot.KeyFound);
        Assert.Equal(4, Assert.Single(snapshot.Entries).Index);
    }
}
