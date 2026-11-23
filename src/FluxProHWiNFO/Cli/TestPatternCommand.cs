using System.Globalization;
using FluxProHWiNFO.Display;

namespace FluxProHWiNFO.Cli;

/// <summary>
/// <c>--test-pattern</c>: sends fixed values (default CPU 12.3, GPU 45.6) once a second for a while,
/// to confirm the protocol and report framing on real hardware. <c>--blank</c> sends EE EE EE for both.
/// </summary>
internal static class TestPatternCommand
{
    public const string Help = """
          --test-pattern [options]  Send fixed values to the display once a second
              --variant <name>      Report framing: id0-pad (default), id0, raw-pad, feature
              --cpu <value>         CPU value to show (default 12.3)
              --gpu <value>         GPU value to show (default 45.6)
              --seconds <n>         How long to keep sending (default 30; 0 = one write)
          --blank [--variant <name>] Send blanks (EE) for both values once
        """;

    public static int Run(TextWriter output, string[] args, bool blank)
    {
        var framing = ReportFraming.ReportIdZeroPadded;
        double? cpu = 12.3, gpu = 45.6;
        int seconds = 30;

        for (int i = 1; i < args.Length; i++)
        {
            string opt = args[i].ToLowerInvariant();
            string? val = i + 1 < args.Length ? args[i + 1] : null;
            switch (opt)
            {
                case "--variant" when val is not null && HidReportBuilder.CliNames.TryGetValue(val, out var f):
                    framing = f; i++; break;
                case "--cpu" when TryParse(val, out double c):
                    cpu = c; i++; break;
                case "--gpu" when TryParse(val, out double g):
                    gpu = g; i++; break;
                case "--seconds" when int.TryParse(val, NumberStyles.None, CultureInfo.InvariantCulture, out int s):
                    seconds = s; i++; break;
                default:
                    output.WriteLine($"Bad or incomplete option: {args[i]}{(val is null ? "" : " " + val)}");
                    output.WriteLine(Help);
                    return 64;
            }
        }
        if (blank)
        {
            cpu = gpu = null;
            seconds = 0;
        }

        HidInfoCommand.WarnAboutConflicts(output);
        var device = FluxDisplayDevice.FindWritable();
        if (device is null)
        {
            output.WriteLine("Display not found (no VID 2022 / PID 0522 HID interface with output or feature reports).");
            return 1;
        }

        byte[] payload = PayloadEncoder.Encode(cpu, gpu);
        output.WriteLine($"Device  : {device.DevicePath}");
        output.WriteLine($"Variant : {HidReportBuilder.CliName(framing)}");
        output.WriteLine($"Values  : CPU {Show(cpu)}, GPU {Show(gpu)}");
        output.WriteLine($"Payload : {PayloadEncoder.ToHex(payload)}");

        try
        {
            using var display = FluxDisplayDevice.Open(device, framing);
            output.WriteLine($"Report length for this variant: {display.ReportLength} bytes");

            var started = DateTime.UtcNow;
            int writes = 0;
            do
            {
                byte[] sent = display.Send(payload);
                if (writes++ == 0)
                    output.WriteLine($"Report  : {PayloadEncoder.ToHex(sent)}  ({sent.Length} bytes) - write OK");
                if (seconds > 0)
                    Thread.Sleep(1000);
            }
            while (DateTime.UtcNow - started < TimeSpan.FromSeconds(seconds));

            output.WriteLine($"Done: {writes} write(s) without error. The display keeps whatever it shows now.");
            return 0;
        }
        catch (Exception ex)
        {
            output.WriteLine($"Write FAILED: {ex.GetType().Name}: {ex.Message}");
            return 3;
        }
    }

    private static bool TryParse(string? s, out double value) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string Show(double? v) => v?.ToString("0.0", CultureInfo.InvariantCulture) ?? "blank";
}
