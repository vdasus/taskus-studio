using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskusStudio.Model;

public enum ThemeChoice { System, Light, Dark }

/// <summary>Editor settings, kept in taskus-studio.json next to the executable or, when that folder is read-only, in the user folder.</summary>
public sealed class AppSettings
{
    public const string FILE_NAME = "taskus-studio.json";
    private const int MAX_RECENT = 10;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string TaskusPath { get; set; } = "";

    public string SecretsPath { get; set; } = "";

    public List<string> RecentFiles { get; set; } = [];

    public ThemeChoice Theme { get; set; } = ThemeChoice.System;

    /// <summary>Relative widths of the Source, Map and Sink panels as the user left them; empty: the layout default.</summary>
    public List<double> PanelWidths { get; set; } = [];

    [JsonIgnore]
    public string FilePath { get; private set; } = "";

    public static AppSettings Load(string appFolder, string userFolder)
    {
        var path = Location(appFolder, userFolder);
        AppSettings settings;
        try
        {
            settings = File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new() : new();
        }
        catch (JsonException)
        {
            settings = new(); // a broken file is replaced on the next save
        }

        settings.FilePath = path;
        return settings;
    }

    public void Save()
    {
        if (FilePath.Length == 0)
            return; // settings made in memory (designer, tests) have no file
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }

    public void AddRecent(string file)
    {
        RecentFiles.RemoveAll(f => string.Equals(f, file, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, file);
        if (RecentFiles.Count > MAX_RECENT)
            RecentFiles.RemoveRange(MAX_RECENT, RecentFiles.Count - MAX_RECENT);
    }

    /// <summary>The file next to the executable when that folder is writable, else the one in the user folder.</summary>
    public static string Location(string appFolder, string userFolder)
    {
        var local = Path.Combine(appFolder, FILE_NAME);
        if (File.Exists(local) ? CanWrite(local) : CanCreate(appFolder))
            return local;
        return Path.Combine(userFolder, FILE_NAME);
    }

    public static string UserFolder() =>
        OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support", "TaskusStudio")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskusStudio");

    private static bool CanWrite(string file)
    {
        try
        {
            using var _ = File.Open(file, FileMode.Open, FileAccess.ReadWrite);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool CanCreate(string folder)
    {
        var probe = Path.Combine(folder, $".taskus-studio-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
