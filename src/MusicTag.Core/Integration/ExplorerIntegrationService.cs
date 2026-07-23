namespace MusicTag.Core.Integration;

/// <summary>
/// Implements the exact HKCU registry layout from plan section 7:
/// <code>
/// Software\Classes\Directory\shell\MusicTag\(Default) = "Open with MusicTag"
/// Software\Classes\Directory\shell\MusicTag\Icon = "&lt;exePath&gt;,0"
/// Software\Classes\Directory\shell\MusicTag\command\(Default) = "&lt;exePath&gt;" "%1"
/// Software\Classes\Directory\Background\shell\MusicTag\(Default) = "Open with MusicTag"
/// Software\Classes\Directory\Background\shell\MusicTag\Icon = "&lt;exePath&gt;,0"
/// Software\Classes\Directory\Background\shell\MusicTag\command\(Default) = "&lt;exePath&gt;" "%V"
/// </code>
/// The "Icon" value (per user feedback — "Open with MusicTag" showed no icon in the context
/// menu) is what Explorer reads to show a menu-item icon; it lives on the same key as the
/// display-text default value, not the \command subkey. Format is "&lt;path&gt;,&lt;index&gt;" —
/// deliberately unquoted around the path (unlike the command values above): Explorer parses
/// this by splitting on the last comma, not as a command line, so wrapping it in quotes would
/// become part of the parsed path instead of being stripped. Index 0 selects the exe's own
/// embedded icon (MusicTag.App.csproj's ApplicationIcon), so this never goes stale relative to
/// whatever icon the exe actually has. All real <c>Microsoft.Win32.Registry</c> access is
/// behind <see cref="IRegistryKeyWrapper"/> (see <see cref="ExplorerIntegrationServiceTests"/>,
/// which asserts these exact strings against a fake implementation without touching the real
/// registry).
/// </summary>
public sealed class ExplorerIntegrationService : IExplorerIntegrationService
{
    private const string MenuText = "Open with MusicTag";

    private const string ShellKeyPath = @"Software\Classes\Directory\shell\MusicTag";
    private const string ShellCommandKeyPath = @"Software\Classes\Directory\shell\MusicTag\command";

    private const string BackgroundShellKeyPath = @"Software\Classes\Directory\Background\shell\MusicTag";
    private const string BackgroundShellCommandKeyPath = @"Software\Classes\Directory\Background\shell\MusicTag\command";

    private readonly IRegistryKeyWrapper _registry;

    public ExplorerIntegrationService(IRegistryKeyWrapper registry)
    {
        _registry = registry;
    }

    public bool IsRegistered() => _registry.GetDefaultValue(ShellKeyPath) is not null;

    public void Register()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the current process's executable path.");

        // Double-quoted per plan section 7 — exePath can contain spaces (e.g. under
        // "C:\Program Files\..."), and %1/%V are supplied by Explorer unquoted, so each gets
        // its own quote pair rather than relying on one pair to cover the whole command line.
        var directoryCommand = $"\"{exePath}\" \"%1\"";
        var backgroundCommand = $"\"{exePath}\" \"%V\"";

        // Unquoted — see this class's doc comment on why the Icon value's path isn't wrapped
        // in quotes the way the command values above are.
        var iconValue = $"{exePath},0";

        _registry.SetDefaultValue(ShellKeyPath, MenuText);
        _registry.SetNamedValue(ShellKeyPath, "Icon", iconValue);
        _registry.SetDefaultValue(ShellCommandKeyPath, directoryCommand);

        _registry.SetDefaultValue(BackgroundShellKeyPath, MenuText);
        _registry.SetNamedValue(BackgroundShellKeyPath, "Icon", iconValue);
        _registry.SetDefaultValue(BackgroundShellCommandKeyPath, backgroundCommand);
    }

    public void Unregister()
    {
        _registry.DeleteTree(ShellKeyPath);
        _registry.DeleteTree(BackgroundShellKeyPath);
    }
}
