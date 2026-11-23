using HidSharp;

namespace FluxProHWiNFO.Display;

/// <summary>The Antec Flux Pro side-panel display, opened as a HID device (VID 0x2022, PID 0x0522).</summary>
public sealed class FluxDisplayDevice : IDisposable
{
    public const int VendorId = 0x2022;
    public const int ProductId = 0x0522;
    public const int WriteTimeoutMs = 1000;

    private readonly HidStream _stream;

    public HidDevice Device { get; }
    public ReportFraming Framing { get; }

    private FluxDisplayDevice(HidDevice device, HidStream stream, ReportFraming framing)
    {
        Device = device;
        _stream = stream;
        Framing = framing;
    }

    public static IReadOnlyList<HidDevice> FindAll() =>
        DeviceList.Local.GetHidDevices(VendorId, ProductId).ToList();

    /// <summary>The matching HID interface that accepts output reports (largest output report wins), or null.</summary>
    public static HidDevice? FindWritable() =>
        FindAll()
            .Select(d => (Device: d, OutLen: TryGet(d.GetMaxOutputReportLength), FeatLen: TryGet(d.GetMaxFeatureReportLength)))
            .Where(x => x.OutLen > 0 || x.FeatLen > 0)
            .OrderByDescending(x => x.OutLen)
            .Select(x => x.Device)
            .FirstOrDefault();

    public static FluxDisplayDevice Open(HidDevice device, ReportFraming framing)
    {
        var stream = device.Open();
        stream.WriteTimeout = WriteTimeoutMs;
        return new FluxDisplayDevice(device, stream, framing);
    }

    public int ReportLength => HidReportBuilder.IsFeature(Framing)
        ? TryGet(Device.GetMaxFeatureReportLength)
        : TryGet(Device.GetMaxOutputReportLength);

    /// <summary>Frames and writes one payload. Returns the exact bytes handed to the HID stack.</summary>
    public byte[] Send(ReadOnlySpan<byte> payload)
    {
        byte[] report = HidReportBuilder.Build(payload, Framing, ReportLength);
        if (HidReportBuilder.IsFeature(Framing))
            _stream.SetFeature(report);
        else
            _stream.Write(report);
        return report;
    }

    public void Dispose() => _stream.Dispose();

    internal static int TryGet(Func<int> getter)
    {
        try { return getter(); }
        catch (Exception) { return 0; }
    }
}
