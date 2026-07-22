using System.Text.Json;

namespace MusicTag.Core.Settings;

/// <summary>
/// JSON-file-backed <see cref="ISettingsService"/>, exactly per plan section 6. The real app
/// (see App.xaml.cs's DI wiring) always uses the parameterless constructor, which resolves to
/// <c>%AppData%\MusicTag\settings.json</c>; the overload taking an explicit file path exists
/// purely so <c>SettingsServiceTests</c> can exercise Load/Save/corrupt-file-fallback against a
/// throwaway temp file instead of the real user's actual settings — the same "inject a seam for
/// testability" pattern the plan already uses for <see cref="Integration.IRegistryKeyWrapper"/>.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsFilePath;

    public SettingsService()
        : this(GetDefaultSettingsFilePath())
    {
    }

    public SettingsService(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    private static string GetDefaultSettingsFilePath()
    {
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataFolder, "MusicTag", "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupt/hand-edited/unreadable file: never crash the app over it. Best-effort
            // rename to a .bak sibling (so the user doesn't silently lose whatever was in
            // there) and fall back to defaults; if even the rename fails (e.g. permissions),
            // swallow that too and still return defaults rather than propagating.
            TryBackupCorruptFile();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempFilePath = _settingsFilePath + ".tmp";
        File.WriteAllText(tempFilePath, json);

        // Atomic swap: File.Replace requires the destination to already exist (it needs
        // something to replace), so the very first save in the app's lifetime — when
        // settings.json doesn't exist yet — falls back to a plain move instead.
        if (File.Exists(_settingsFilePath))
        {
            File.Replace(tempFilePath, _settingsFilePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempFilePath, _settingsFilePath);
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            var backupFilePath = _settingsFilePath + ".bak";
            File.Move(_settingsFilePath, backupFilePath, overwrite: true);
        }
        catch
        {
            // Best-effort only — see Load()'s doc comment. Never let a failed backup attempt
            // turn into a crash on top of the original corruption.
        }
    }
}
