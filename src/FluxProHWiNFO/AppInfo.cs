using System.Reflection;

namespace FluxProHWiNFO;

internal static class AppInfo
{
    public const string Name = "FluxProHWiNFO";

    /// <summary>Per-session single-instance mutex. CLI test commands check it so they don't fight the tray app.</summary>
    public const string MutexName = @"Local\FluxProHWiNFO.SingleInstance";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public static string AppDataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);

    public static bool IsTrayInstanceRunning()
    {
        if (!Mutex.TryOpenExisting(MutexName, out var mutex))
            return false;
        mutex.Dispose();
        return true;
    }
}
