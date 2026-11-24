namespace FluxProHWiNFO.Startup;

/// <summary>Builds and reads the command line stored in a Run value.</summary>
public static class RunCommand
{
    /// <summary>
    /// Argument that marks a launch from Windows sign-in. With it, a second copy exits silently instead of
    /// showing "already running", which matters when both a per-user and an all-users entry exist.
    /// </summary>
    public const string BackgroundArg = "--background";

    public static string Build(string exePath) => $"\"{exePath}\" {BackgroundArg}";

    /// <summary>The exe path of a Run value: the quoted part, or everything up to the first space.</summary>
    public static string ExePathOf(string command)
    {
        string c = command.Trim();
        if (c.StartsWith('"'))
        {
            int end = c.IndexOf('"', 1);
            return end < 0 ? c[1..] : c[1..end];
        }
        int space = c.IndexOf(' ');
        return space < 0 ? c : c[..space];
    }

    /// <summary>
    /// True when the command starts <paramref name="exePath"/>, with or without arguments. Entries written
    /// by version 0.1.0 had no arguments and still count.
    /// </summary>
    public static bool PointsAt(string command, string? exePath) =>
        !string.IsNullOrEmpty(exePath) && string.Equals(ExePathOf(command), exePath, StringComparison.OrdinalIgnoreCase);
}
