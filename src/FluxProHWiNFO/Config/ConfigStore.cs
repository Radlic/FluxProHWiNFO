using System.Text.Json;

namespace FluxProHWiNFO.Config;

public static class ConfigStore
{
    public const string FileName = "config.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    /// <summary>
    /// A config.json next to the exe wins (portable use); otherwise %APPDATA%\FluxProHWiNFO\config.json.
    /// </summary>
    public static string ResolvePath(string exeDir, string appDataDir)
    {
        string portable = Path.Combine(exeDir, FileName);
        return File.Exists(portable) ? portable : Path.Combine(appDataDir, FileName);
    }

    /// <summary>
    /// Loads the config, creating the file with defaults if it doesn't exist.
    /// Returns (null, error) if the file can't be read or parsed, so the caller can keep its previous settings.
    /// </summary>
    public static (AppConfig? Config, string? Error) LoadOrCreate(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                var defaults = new AppConfig();
                Save(path, defaults);
                return (defaults, null);
            }

            string json = File.ReadAllText(path);
            var config = string.IsNullOrWhiteSpace(json) ? new AppConfig() : JsonSerializer.Deserialize<AppConfig>(json, Options);
            return config is null ? (null, "file contains null") : (config.Normalize(), null);
        }
        catch (JsonException ex)
        {
            return (null, $"invalid JSON: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, ex.Message);
        }
    }

    public static void Save(string path, AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
    }
}
