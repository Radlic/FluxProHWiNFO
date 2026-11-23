namespace FluxProHWiNFO.Logging;

/// <summary>
/// Small rolling text log. Callers log state changes, not every tick, to keep it quiet.
/// When the file passes <see cref="MaxBytes"/> it moves to FluxProHWiNFO.1.log (one backup kept).
/// Logging never throws.
/// </summary>
public static class Log
{
    public const long MaxBytes = 512 * 1024;

    private static readonly Lock Gate = new();
    private static readonly System.Text.UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static string? FilePath { get; private set; }

    public static void Init(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory, AppInfo.Name + ".log");
        }
        catch (Exception)
        {
            FilePath = null;
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        string? path = FilePath;
        if (path is null)
            return;

        lock (Gate)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Exists && info.Length > MaxBytes)
                    File.Move(path, Path.ChangeExtension(path, ".1.log"), overwrite: true);

                // UTF-8 with BOM (written only when the file is new) so Windows PowerShell 5.1 and old editors read "°" correctly.
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level,-5} {message}{Environment.NewLine}", Utf8WithBom);
            }
            catch (Exception)
            {
                // A log write must never take the app down.
            }
        }
    }
}
