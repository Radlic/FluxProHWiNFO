namespace FluxProHWiNFO.Display;

/// <summary>How the 12-byte payload is wrapped into a HID report. Which one the display wants is confirmed on real hardware.</summary>
public enum ReportFraming
{
    /// <summary>Report ID 0x00, then the payload, zero-padded to the output report length. The usual Windows convention for devices without report IDs.</summary>
    ReportIdZeroPadded,

    /// <summary>Report ID 0x00, then the payload, no explicit padding (HidSharp may still pad to the report length when writing).</summary>
    ReportIdZero,

    /// <summary>The payload as is, so 0x55 lands in the report-ID slot, zero-padded to the output report length.</summary>
    NoReportIdPadded,

    /// <summary>Report ID 0x00, then the payload, padded to the feature report length and sent with HidD_SetFeature.</summary>
    FeatureReportIdZero,
}

public static class HidReportBuilder
{
    public static IReadOnlyDictionary<string, ReportFraming> CliNames { get; } = new Dictionary<string, ReportFraming>(StringComparer.OrdinalIgnoreCase)
    {
        ["id0-pad"] = ReportFraming.ReportIdZeroPadded,
        ["id0"] = ReportFraming.ReportIdZero,
        ["raw-pad"] = ReportFraming.NoReportIdPadded,
        ["feature"] = ReportFraming.FeatureReportIdZero,
    };

    public static string CliName(ReportFraming framing) => CliNames.First(kv => kv.Value == framing).Key;

    public static bool IsFeature(ReportFraming framing) => framing == ReportFraming.FeatureReportIdZero;

    /// <param name="reportLength">The device's report length in bytes, including the report-ID byte
    /// (output length, or feature length for <see cref="ReportFraming.FeatureReportIdZero"/>).</param>
    public static byte[] Build(ReadOnlySpan<byte> payload, ReportFraming framing, int reportLength)
    {
        bool prefixReportId = framing != ReportFraming.NoReportIdPadded;
        bool pad = framing != ReportFraming.ReportIdZero;

        int offset = prefixReportId ? 1 : 0;
        int contentLength = offset + payload.Length;
        int length = pad ? Math.Max(contentLength, reportLength) : contentLength;

        var report = new byte[length]; // report ID 0x00 and padding are already zero
        payload.CopyTo(report.AsSpan(offset));
        return report;
    }
}
