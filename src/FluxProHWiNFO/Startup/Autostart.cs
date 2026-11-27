using Microsoft.Win32;

namespace FluxProHWiNFO.Startup;

/// <summary>
/// Start with Windows via a <c>...\CurrentVersion\Run</c> value named <see cref="AppInfo.Name"/>, plus its
/// <c>...\Explorer\StartupApproved\Run</c> value, which is Task Manager's Enabled/Disabled state.
/// <list type="bullet">
/// <item>Per user (HKCU): what the tray toggle and a "just me" install write. No admin rights, not elevated.</item>
/// <item>All users (HKLM): written only by an "all users" install. The app reads it but never writes it,
///       because writing HKLM needs admin rights.</item>
/// </list>
/// Enable() writes the "enabled" approval value, the same thing Task Manager's Enable does, so ticking the
/// toggle also undoes an earlier Disable in Task Manager. An entry with no approval value counts as on,
/// as it does for Windows.
/// Even from an HKLM entry, Windows starts the app as the signed-in user, not elevated.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public static string Command => RunCommand.Build(Environment.ProcessPath ?? "");

    /// <summary>True when this user's entry points at this exe and is approved to run.</summary>
    public static bool IsEnabled() => IsEnabledIn(Registry.CurrentUser);

    /// <summary>True when an all-users install set this exe to start for everyone, approved to run.</summary>
    public static bool IsEnabledForAllUsers() => IsEnabledIn(Registry.LocalMachine);

    /// <summary>True when this user has a Run entry for this exe, approved or not.</summary>
    public static bool HasUserEntry() => RunPointsAtThisExe(Registry.CurrentUser);

    public static void Enable()
    {
        using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
            run.SetValue(AppInfo.Name, Command, RegistryValueKind.String);
        using (var approved = Registry.CurrentUser.CreateSubKey(ApprovedKey))
            approved.SetValue(AppInfo.Name, StartupApproval.Enabled, RegistryValueKind.Binary);
    }

    public static void Disable()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
            run?.DeleteValue(AppInfo.Name, throwOnMissingValue: false);
        using (var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true))
            approved?.DeleteValue(AppInfo.Name, throwOnMissingValue: false);
    }

    private static bool IsEnabledIn(RegistryKey root)
    {
        if (!RunPointsAtThisExe(root))
            return false;
        using var approved = root.OpenSubKey(ApprovedKey);
        return StartupApproval.IsEnabled(approved?.GetValue(AppInfo.Name) as byte[]);
    }

    private static bool RunPointsAtThisExe(RegistryKey root)
    {
        using var run = root.OpenSubKey(RunKey);
        return run?.GetValue(AppInfo.Name) is string value && RunCommand.PointsAt(value, Environment.ProcessPath);
    }
}
