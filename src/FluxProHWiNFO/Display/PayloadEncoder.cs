namespace FluxProHWiNFO.Display;

/// <summary>
/// Builds the 12-byte packet the Antec Flux Pro display expects:
/// <c>55 AA 01 01 06 | CPU tens units tenths | GPU tens units tenths | checksum</c>.
/// </summary>
/// <remarks>
/// Values are rounded (not truncated) to one decimal place and clamped to 0.0–99.9.
/// Anything at or above 99.95 shows as 99.9 and anything below 0 shows as 0.0.
/// A missing, NaN or infinite value is sent as <c>EE EE EE</c>, which the display shows as blank.
/// </remarks>
public static class PayloadEncoder
{
    public const int PayloadLength = 12;
    public const byte MissingDigit = 0xEE;
    public const int MaxTenths = 999;

    private static ReadOnlySpan<byte> Header => [0x55, 0xAA, 0x01, 0x01, 0x06];

    public static byte[] Encode(double? cpu, double? gpu)
    {
        var payload = new byte[PayloadLength];
        Header.CopyTo(payload);
        WriteDigits(payload.AsSpan(5, 3), ToTenths(cpu));
        WriteDigits(payload.AsSpan(8, 3), ToTenths(gpu));

        int sum = 0;
        for (int i = 0; i < PayloadLength - 1; i++)
            sum += payload[i];
        payload[PayloadLength - 1] = (byte)(sum & 0xFF);
        return payload;
    }

    /// <summary>Blank display: both values missing.</summary>
    public static byte[] EncodeBlank() => Encode(null, null);

    /// <summary>
    /// Converts a temperature to whole tenths (24.66 → 247), clamped to 0–999.
    /// Returns null for a missing or non-finite value.
    /// </summary>
    public static int? ToTenths(double? value)
    {
        if (value is not double v || !double.IsFinite(v))
            return null;
        double tenths = Math.Round(v * 10.0, MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(tenths, 0, MaxTenths);
    }

    public static string ToHex(ReadOnlySpan<byte> bytes) =>
        string.Join(' ', bytes.ToArray().Select(b => b.ToString("X2")));

    private static void WriteDigits(Span<byte> target, int? tenths)
    {
        if (tenths is not int t)
        {
            target.Fill(MissingDigit);
            return;
        }
        target[0] = (byte)(t / 100);
        target[1] = (byte)(t / 10 % 10);
        target[2] = (byte)(t % 10);
    }
}
