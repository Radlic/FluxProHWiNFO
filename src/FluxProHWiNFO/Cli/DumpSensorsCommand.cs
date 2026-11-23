using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using FluxProHWiNFO.Display;
using FluxProHWiNFO.HWiNFO;

namespace FluxProHWiNFO.Cli;

/// <summary><c>--dump-sensors</c>: prints every VSB entry and what the default labels would match.</summary>
internal static class DumpSensorsCommand
{
    public static int Run(TextWriter output)
    {
        var processes = Process.GetProcessesByName("HWiNFO64");
        output.WriteLine(processes.Length > 0
            ? $"HWiNFO64 process : running (PID {string.Join(", ", processes.Select(p => p.Id))})"
            : "HWiNFO64 process : NOT running");
        foreach (var p in processes) p.Dispose();

        using (var identity = WindowsIdentity.GetCurrent())
        {
            bool elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            output.WriteLine($"Running as       : {identity.Name}{(elevated ? " (elevated)" : "")}");
        }

        VsbSnapshot snapshot;
        try
        {
            snapshot = new VsbReader().Read();
        }
        catch (Exception ex)
        {
            output.WriteLine($"Registry key     : {RegistryVsbSource.DisplayPath} could not be read: {ex.Message}");
            return 2;
        }

        if (!snapshot.KeyFound)
        {
            output.WriteLine($"Registry key     : {RegistryVsbSource.DisplayPath} NOT found");
            output.WriteLine();
            output.WriteLine("In HWiNFO64 Sensor Settings > HWiNFO Gadget, tick \"Enable reporting to Gadget\",");
            output.WriteLine("then select each sensor you want and tick \"Report value in Gadget\".");
            return 1;
        }

        output.WriteLine($"Registry key     : {RegistryVsbSource.DisplayPath} ({snapshot.Entries.Count} entries)");
        output.WriteLine();

        if (snapshot.Entries.Count > 0)
            WriteTable(output, snapshot.Entries);
        else
            output.WriteLine("(no entries - no sensor has \"Report value in Gadget\" ticked)");

        output.WriteLine();
        var cpu = SensorSelector.DefaultCpu.FindIn(snapshot.Entries);
        var gpu = SensorSelector.DefaultGpu.FindIn(snapshot.Entries);
        WriteMatch(output, "CPU", SensorSelector.DefaultCpu, cpu);
        WriteMatch(output, "GPU", SensorSelector.DefaultGpu, gpu);
        output.WriteLine();
        output.WriteLine($"Payload (not sent): {PayloadEncoder.ToHex(PayloadEncoder.Encode(cpu?.Value, gpu?.Value))}");
        return 0;
    }

    private static void WriteTable(TextWriter output, IReadOnlyList<VsbEntry> entries)
    {
        string[] headers = ["Idx", "Sensor", "Label", "Value", "ValueRaw"];
        var rows = entries
            .Select(e => new[] { e.Index.ToString(CultureInfo.InvariantCulture), e.Sensor ?? "", e.Label ?? "", e.Value ?? "", e.ValueRaw ?? "" })
            .ToList();

        int[] widths = headers.Select((h, i) => Math.Max(h.Length, rows.Max(r => r[i].Length))).ToArray();
        string Format(string[] cells) => string.Join("  ", cells.Select((c, i) => c.PadRight(widths[i]))).TrimEnd();

        output.WriteLine(Format(headers));
        output.WriteLine(Format(widths.Select(w => new string('-', w)).ToArray()));
        foreach (var r in rows)
            output.WriteLine(Format(r));
    }

    private static void WriteMatch(TextWriter output, string name, SensorSelector selector, SensorMatch? match)
    {
        if (match is null)
        {
            output.WriteLine($"{name} match: none (looked for {selector})");
            return;
        }
        int tenths = PayloadEncoder.ToTenths(match.Value)!.Value;
        output.WriteLine(
            $"{name} match: [{match.Entry.Index}] \"{match.Entry.Label}\" from \"{match.Entry.Sensor}\" = " +
            $"{match.Value.ToString(CultureInfo.InvariantCulture)} -> display {tenths / 10}.{tenths % 10}");
    }
}
