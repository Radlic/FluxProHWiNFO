using FluxProHWiNFO.Config;
using FluxProHWiNFO.HWiNFO;

namespace FluxProHWiNFO.Core;

public enum HwinfoStatus
{
    Ok,
    NotRunning,
    /// <summary>HKCU\Software\HWiNFO64\VSB doesn't exist: Gadget reporting is off.</summary>
    KeyMissing,
    ReadError,
}

/// <param name="CpuDisplay">Value to show, already in the configured unit; null shows dashes.</param>
public sealed record Readings(HwinfoStatus Status, SensorMatch? Cpu, SensorMatch? Gpu, double? CpuDisplay, double? GpuDisplay)
{
    public static Readings Blank(HwinfoStatus status) => new(status, null, null, null, null);
}

public static class ReadingResolver
{
    /// <param name="snapshot">Null if reading the registry failed.</param>
    public static Readings Resolve(VsbSnapshot? snapshot, bool hwinfoRunning, AppConfig config)
    {
        // HWiNFO may leave its last values in the registry when it exits, so trust them only while it runs.
        if (!hwinfoRunning)
            return Readings.Blank(HwinfoStatus.NotRunning);
        if (snapshot is null)
            return Readings.Blank(HwinfoStatus.ReadError);
        if (!snapshot.KeyFound)
            return Readings.Blank(HwinfoStatus.KeyMissing);

        var cpu = config.Cpu.ToSelector().FindIn(snapshot.Entries);
        var gpu = config.Gpu.ToSelector().FindIn(snapshot.Entries);
        return new Readings(
            HwinfoStatus.Ok,
            cpu,
            gpu,
            Temperature.ForDisplay(cpu, config.Fahrenheit),
            Temperature.ForDisplay(gpu, config.Fahrenheit));
    }
}

public static class Temperature
{
    /// <summary>True when HWiNFO itself is set to Fahrenheit for this value (its formatted Value ends in °F).</summary>
    public static bool IsFahrenheit(VsbEntry entry) =>
        entry.Value?.Contains("°F", StringComparison.OrdinalIgnoreCase) == true;

    public static double ToCelsius(SensorMatch match) =>
        IsFahrenheit(match.Entry) ? (match.Value - 32.0) * 5.0 / 9.0 : match.Value;

    public static double? ForDisplay(SensorMatch? match, bool fahrenheit)
    {
        if (match is null)
            return null;
        double c = ToCelsius(match);
        return fahrenheit ? c * 9.0 / 5.0 + 32.0 : c;
    }
}
