using MusicTag.Core.Integration;

namespace MusicTag.Tests;

/// <summary>
/// Asserts the exact <c>.desktop</c>-entry and Nautilus-script paths/content against a fake
/// <see cref="ILinuxDesktopFileWriter"/> — never touches the real home directory, mirroring
/// <see cref="ExplorerIntegrationServiceTests"/>'s testability treatment of the registry.
/// </summary>
public class LinuxFileManagerIntegrationServiceTests
{
    private const string HomeDirectory = "/home/testuser";
    private const string DesktopEntryPath = HomeDirectory + "/.local/share/applications/musictag.desktop";
    private const string NautilusScriptPath = HomeDirectory + "/.local/share/nautilus/scripts/Open with MusicTag";

    [Fact]
    public void Register_WritesDesktopEntryAndNautilusScript()
    {
        var writer = new FakeLinuxDesktopFileWriter();
        var service = new LinuxFileManagerIntegrationService(writer);
        var expectedExePath = Environment.ProcessPath!;

        service.Register();

        Assert.True(writer.Files.ContainsKey(DesktopEntryPath));
        Assert.Contains($"Exec=\"{expectedExePath}\" %f", writer.Files[DesktopEntryPath]);
        Assert.Contains("MimeType=inode/directory;", writer.Files[DesktopEntryPath]);
        Assert.True(writer.ExecutableFlags[DesktopEntryPath]);

        Assert.True(writer.Files.ContainsKey(NautilusScriptPath));
        var script = writer.Files[NautilusScriptPath];
        Assert.Contains($"exec \"{expectedExePath}\" \"$target\"", script);
        Assert.True(writer.ExecutableFlags[NautilusScriptPath]);

        // Multi-select / individual-file support: every selected path (not just the first)
        // must be forwarded as its own argv entry via "$@", so a Nautilus selection of one or
        // more audio files reaches App.ResolveStartupFolder intact rather than being truncated
        // to a single item.
        Assert.Contains("IFS=$'\\n' set -- $NAUTILUS_SCRIPT_SELECTED_FILE_PATHS", script);
        Assert.Contains($"exec \"{expectedExePath}\" \"$@\"", script);
        Assert.DoesNotContain("head -n1", script);
    }

    [Fact]
    public void IsRegistered_FalseBeforeRegister_TrueAfter()
    {
        var writer = new FakeLinuxDesktopFileWriter();
        var service = new LinuxFileManagerIntegrationService(writer);

        Assert.False(service.IsRegistered());

        service.Register();

        Assert.True(service.IsRegistered());
    }

    [Fact]
    public void Unregister_RemovesBothFiles()
    {
        var writer = new FakeLinuxDesktopFileWriter();
        var service = new LinuxFileManagerIntegrationService(writer);
        service.Register();

        service.Unregister();

        Assert.False(service.IsRegistered());
        Assert.False(writer.Files.ContainsKey(DesktopEntryPath));
        Assert.False(writer.Files.ContainsKey(NautilusScriptPath));
    }

    [Fact]
    public void Unregister_WithoutPriorRegister_DoesNotThrow()
    {
        var writer = new FakeLinuxDesktopFileWriter();
        var service = new LinuxFileManagerIntegrationService(writer);

        var exception = Record.Exception(() => service.Unregister());

        Assert.Null(exception);
    }

    [Fact]
    public void Register_IsIdempotent_RunningTwiceLeavesTheSameTwoFiles()
    {
        var writer = new FakeLinuxDesktopFileWriter();
        var service = new LinuxFileManagerIntegrationService(writer);

        service.Register();
        service.Register();

        Assert.Equal(2, writer.Files.Count);
        Assert.True(service.IsRegistered());
    }

    /// <summary>In-memory stand-in for the real home directory's filesystem, keyed by full path.</summary>
    private sealed class FakeLinuxDesktopFileWriter : ILinuxDesktopFileWriter
    {
        public Dictionary<string, string> Files { get; } = new();

        public Dictionary<string, bool> ExecutableFlags { get; } = new();

        public string GetHomeDirectory() => HomeDirectory;

        public bool FileExists(string path) => Files.ContainsKey(path);

        public void WriteFile(string path, string content, bool executable)
        {
            Files[path] = content;
            ExecutableFlags[path] = executable;
        }

        public void DeleteFile(string path)
        {
            Files.Remove(path);
            ExecutableFlags.Remove(path);
        }
    }
}
