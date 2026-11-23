using Microsoft.Win32;

namespace FluxProHWiNFO.Startup;

/// <summary>
/// Start with Windows via HKCU\...\CurrentVersion\Run: per user, no admin rights, not elevated.
/// This is the same mechanism as a Startup-folder shortcut, and it shows up in Task Manager's
/// Startup apps tab, where the user can also turn it off.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>True when the Run entry points at this exe and Task Manager hasn't disabled it.</summary>
    public static bool IsEnabled()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey);
        if (run?.GetValue(AppInfo.Name) is not string value || !string.Equals(value, Command, StringComparison.OrdinalIgnoreCase))
            return false;

        // Task Manager marks a disabled entry with an odd first byte (0x03 / 0x07).
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
        return approved?.GetValue(AppInfo.Name) is not byte[] { Length: > 0 } flags || (flags[0] & 1) == 0;
    }

    public static void Enable()
    {
        using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
            run.SetValue(AppInfo.Name, Command, RegistryValueKind.String);

        // Clear a "disabled" mark left by Task Manager so the entry actually runs.
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        approved?.DeleteValue(AppInfo.Name, throwOnMissingValue: false);
    }

    public static void Disable()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        run?.DeleteValue(AppInfo.Name, throwOnMissingValue: false);
    }
}
