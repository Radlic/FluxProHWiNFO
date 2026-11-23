using System.Diagnostics;
using FluxProHWiNFO.Display;
using HidSharp;
using HidSharp.Reports;

namespace FluxProHWiNFO.Cli;

/// <summary><c>--hid-info</c>: prints the display's HID capabilities. Read-only: nothing is written to the device.</summary>
internal static class HidInfoCommand
{
    public static int Run(TextWriter output)
    {
        var devices = FluxDisplayDevice.FindAll();
        output.WriteLine($"HID devices with VID {FluxDisplayDevice.VendorId:X4} PID {FluxDisplayDevice.ProductId:X4}: {devices.Count}");
        WarnAboutConflicts(output);

        if (devices.Count == 0)
        {
            output.WriteLine("Display not found. Check the case's internal USB cable, and Device Manager for 'HID-compliant vendor-defined device'.");
            return 1;
        }

        foreach (var d in devices)
            WriteDevice(output, d);

        var writable = FluxDisplayDevice.FindWritable();
        output.WriteLine(writable is null
            ? "No interface accepts output or feature reports."
            : $"Would write to: {writable.DevicePath}");
        return writable is null ? 1 : 0;
    }

    internal static void WarnAboutConflicts(TextWriter output)
    {
        var others = Process.GetProcesses()
            .Where(p => p.ProcessName.Contains("iUnity", StringComparison.OrdinalIgnoreCase)
                     || p.ProcessName.Contains("FluxProDisplay", StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.ProcessName} (PID {p.Id})")
            .ToList();
        if (others.Count > 0)
            output.WriteLine($"WARNING: other display software is running and may fight over the display: {string.Join(", ", others)}");
    }

    private static void WriteDevice(TextWriter output, HidDevice d)
    {
        output.WriteLine();
        output.WriteLine($"Path            : {d.DevicePath}");
        output.WriteLine($"Manufacturer    : {Try(d.GetManufacturer)}");
        output.WriteLine($"Product         : {Try(d.GetProductName)}");
        output.WriteLine($"Friendly name   : {Try(d.GetFriendlyName)}");
        output.WriteLine($"Release (BCD)   : {d.ReleaseNumberBcd:X4}");
        output.WriteLine($"Report lengths  : input {FluxDisplayDevice.TryGet(d.GetMaxInputReportLength)}, " +
                         $"output {FluxDisplayDevice.TryGet(d.GetMaxOutputReportLength)}, " +
                         $"feature {FluxDisplayDevice.TryGet(d.GetMaxFeatureReportLength)}  (bytes, including the report-ID byte)");

        byte[]? raw = null;
        try { raw = d.GetRawReportDescriptor(); }
        catch (Exception ex) { output.WriteLine($"Report descriptor: unavailable ({ex.Message})"); }
        if (raw is null)
            return;

        output.WriteLine($"Report descriptor ({raw.Length} bytes): {PayloadEncoder.ToHex(raw)}");

        try
        {
            var descriptor = new ReportDescriptor(raw);
            foreach (var item in descriptor.DeviceItems)
            {
                var usages = item.Usages.GetAllValues().Select(u => $"page 0x{u >> 16:X4} usage 0x{u & 0xFFFF:X4}");
                output.WriteLine($"Collection      : {string.Join(", ", usages)}");
            }
            foreach (var report in descriptor.Reports)
            {
                output.WriteLine($"  {report.ReportType,-7} report ID 0x{report.ReportID:X2}, length {report.Length} bytes " +
                                 $"({(report.ReportID == 0 ? "device uses no report IDs" : "explicit report ID")})");
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"Could not parse report descriptor: {ex.Message}");
        }
    }

    private static string Try(Func<string> getter)
    {
        try { return getter(); }
        catch (Exception ex) { return $"(unavailable: {ex.GetType().Name})"; }
    }
}
