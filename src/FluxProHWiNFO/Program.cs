using FluxProHWiNFO.Cli;
using FluxProHWiNFO.Config;
using FluxProHWiNFO.Core;
using FluxProHWiNFO.Logging;
using FluxProHWiNFO.Tray;

namespace FluxProHWiNFO;

static class Program
{
    private static readonly string Usage = $"""
        FluxProHWiNFO {AppInfo.Version} - HWiNFO64 temperatures on the Antec Flux Pro display

        Usage:
          FluxProHWiNFO.exe                 Run in the system tray
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
            return RunCli(args);

        using var mutex = new Mutex(initiallyOwned: true, AppInfo.MutexName, out bool firstInstance);
        if (!firstInstance)
        {
            ApplicationConfiguration.Initialize();
            MessageBox.Show($"{AppInfo.Name} is already running. Look for its thermometer icon in the system tray.",
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        return RunTray();
    }

    private static int RunTray()
    {
        Log.Init(AppInfo.AppDataDir);
        string configPath = ConfigStore.ResolvePath(AppContext.BaseDirectory, AppInfo.AppDataDir);
        Log.Info($"{AppInfo.Name} {AppInfo.Version} starting ({Environment.ProcessPath}), config {configPath}");

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error("Unhandled UI exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Unhandled exception", e.ExceptionObject as Exception);

        ApplicationConfiguration.Initialize();

        using var worker = new DisplayWorker(configPath);
        worker.Start();
        using (var tray = new TrayAppContext(worker))
            Application.Run(tray);

        worker.Stop(); // sends a final blank frame
        Log.Info($"{AppInfo.Name} exited");
        return 0;
    }

    private static int RunCli(string[] args)
    {
        ConsoleAttach.Ensure();
        string command = args[0].ToLowerInvariant();

        if (command is "--test-pattern" or "--blank" && AppInfo.IsTrayInstanceRunning())
        {
            Console.WriteLine($"{AppInfo.Name} is running in the tray and is writing to the display. Exit it first (tray icon > Exit).");
            return 4;
        }

        switch (command)
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
}
