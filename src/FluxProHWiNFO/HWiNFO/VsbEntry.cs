namespace FluxProHWiNFO.HWiNFO;

/// <summary>One sensor reading HWiNFO publishes under <c>HKCU\Software\HWiNFO64\VSB</c> (index <c>i</c>).</summary>
public sealed record VsbEntry(
    int Index,
    string? Sensor,
    string? Label,
    string? Value,
    string? ValueRaw,
    string? Color)
{
    /// <summary><see cref="ValueRaw"/> parsed as a number, or null when absent or non-numeric.</summary>
    public double? NumericValue => VsbParser.ParseRaw(ValueRaw);
}

/// <summary>Everything read from the VSB key in one pass.</summary>
/// <param name="KeyFound">False when the key doesn't exist (HWiNFO never ran, or Gadget reporting is off).</param>
public sealed record VsbSnapshot(bool KeyFound, IReadOnlyList<VsbEntry> Entries)
{
    public static VsbSnapshot NotFound { get; } = new(false, []);
}
