using MusicTag.Core.Settings;

namespace MusicTag.Tests;

/// <summary>
/// JSON round-trip and corrupt-file fallback for SettingsService, per plan section 9. Every
/// test uses the string-path constructor overload against a throwaway temp file (never the
/// real %AppData%\MusicTag\settings.json) so running these repeatedly never touches — let
/// alone corrupts — the actual user's settings, mirroring AudioFileServiceTests' "operate on a
/// throwaway temp copy" precedent.
/// </summary>
public class SettingsServiceTests
{
    private static string MakeTempSettingsPath()
        => Path.Combine(Path.GetTempPath(), $"musictag-settings-test-{Guid.NewGuid():N}", "settings.json");

    [Fact]
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var path = MakeTempSettingsPath();
        var service = new SettingsService(path);

        var settings = service.Load();

        Assert.Null(settings.DefaultStartupFolder);
        Assert.Equal("System", settings.Theme);
        Assert.Null(settings.LastWindowPlacement);
        Assert.False(settings.ExplorerIntegrationRegistered);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllFields()
    {
        var path = MakeTempSettingsPath();
        try
        {
            var service = new SettingsService(path);
            var original = new AppSettings
            {
                DefaultStartupFolder = @"C:\Music",
                Theme = "Dark",
                LastWindowPlacement = new WindowPlacement(10, 20, 800, 600, IsMaximized: false),
                ExplorerIntegrationRegistered = true,
                GridColumns = new Dictionary<string, GridColumnState>
                {
                    ["TitleColumn"] = new GridColumnState(Visible: true, Width: 142.5),
                    ["GenreColumn"] = new GridColumnState(Visible: false, Width: 80),
                },
            };

            service.Save(original);
            var reloaded = service.Load();

            Assert.Equal(original.DefaultStartupFolder, reloaded.DefaultStartupFolder);
            Assert.Equal(original.Theme, reloaded.Theme);
            Assert.Equal(original.LastWindowPlacement, reloaded.LastWindowPlacement);
            Assert.Equal(original.ExplorerIntegrationRegistered, reloaded.ExplorerIntegrationRegistered);
            Assert.Equal(original.GridColumns, reloaded.GridColumns);
        }
        finally
        {
            CleanUp(path);
        }
    }

    [Fact]
    public void Save_Twice_OverwritesAtomically_AndLoadsLatestValue()
    {
        var path = MakeTempSettingsPath();
        try
        {
            var service = new SettingsService(path);

            service.Save(new AppSettings { Theme = "Light" });
            service.Save(new AppSettings { Theme = "Dark" });

            Assert.Equal("Dark", service.Load().Theme);
            // The atomic-swap temp file must not be left behind after a successful save.
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            CleanUp(path);
        }
    }

    [Fact]
    public void Load_CorruptFile_RenamesToBakAndReturnsDefaults()
    {
        var path = MakeTempSettingsPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ not valid json at all !!!");

            var service = new SettingsService(path);
            var settings = service.Load();

            Assert.Equal("System", settings.Theme);
            Assert.Null(settings.DefaultStartupFolder);

            // The corrupt file itself must be moved out of the way (never silently
            // overwritten/deleted outright) rather than left for the next Load() to trip
            // over again in the same way.
            Assert.False(File.Exists(path));
            Assert.True(File.Exists(path + ".bak"));
            Assert.Equal("{ not valid json at all !!!", File.ReadAllText(path + ".bak"));
        }
        finally
        {
            CleanUp(path);
        }
    }

    [Fact]
    public void Load_CorruptFile_NeverThrows_EvenWhenBakAlreadyExists()
    {
        var path = MakeTempSettingsPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ corrupt again");
            File.WriteAllText(path + ".bak", "stale backup from a previous corrupt-file run");

            var service = new SettingsService(path);
            var exception = Record.Exception(() => service.Load());

            Assert.Null(exception);
        }
        finally
        {
            CleanUp(path);
        }
    }

    private static void CleanUp(string settingsFilePath)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
