using FluxProHWiNFO.Cli;

namespace FluxProHWiNFO;

static class Program
{
    private static readonly string Usage = $"""
        FluxProHWiNFO - HWiNFO64 temperatures on the Antec Flux Pro display

        Usage:
          FluxProHWiNFO.exe                 Run the tray app (not implemented yet)
          FluxProHWiNFO.exe --dump-sensors  List every sensor HWiNFO reports to Gadget, and the default matches
          FluxProHWiNFO.exe --hid-info      Show the display's HID capabilities (writes nothing)
        {TestPatternCommand.Help}
          FluxProHWiNFO.exe --help          Show this help

        Tip: in PowerShell, pipe to Out-Host so the prompt waits for the output:
          .\FluxProHWiNFO.exe --dump-sensors | Out-Host
        """;

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            ConsoleAttach.Ensure();
            switch (args[0].ToLowerInvariant())
            {
                case "--dump-sensors":
                    return DumpSensorsCommand.Run(Console.Out);
                case "--hid-info":
                    return HidInfoCommand.Run(Console.Out);
                case "--test-pattern":
                    return TestPatternCommand.Run(Console.Out, args, blank: false);
                case "--blank":
                    return TestPatternCommand.Run(Console.Out, args, blank: true);
                case "--help" or "-h" or "/?":
                    Console.WriteLine(Usage);
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[0]}");
                    Console.WriteLine(Usage);
                    return 64;
            }
        }

        // Tray mode comes in a later step (polling loop, tray UI, config, logging, autostart).
        ApplicationConfiguration.Initialize();
        MessageBox.Show(Usage, "FluxProHWiNFO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return 0;
    }
}
