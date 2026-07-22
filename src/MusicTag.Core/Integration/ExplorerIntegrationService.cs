namespace MusicTag.Core.Integration;

/// <summary>
/// Implements the exact HKCU registry layout from plan section 7:
/// <code>
/// Software\Classes\Directory\shell\MusicTag\(Default) = "Open with MusicTag"
/// Software\Classes\Directory\shell\MusicTag\command\(Default) = "&lt;exePath&gt;" "%1"
/// Software\Classes\Directory\Background\shell\MusicTag\(Default) = "Open with MusicTag"
/// Software\Classes\Directory\Background\shell\MusicTag\command\(Default) = "&lt;exePath&gt;" "%V"
/// </code>
/// All real <c>Microsoft.Win32.Registry</c> access is behind <see cref="IRegistryKeyWrapper"/>
/// (see <see cref="ExplorerIntegrationServiceTests"/>, which asserts these exact strings against
/// a fake implementation without touching the real registry).
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

        _registry.SetDefaultValue(ShellKeyPath, MenuText);
        _registry.SetDefaultValue(ShellCommandKeyPath, directoryCommand);

        _registry.SetDefaultValue(BackgroundShellKeyPath, MenuText);
        _registry.SetDefaultValue(BackgroundShellCommandKeyPath, backgroundCommand);
    }

    public void Unregister()
    {
        _registry.DeleteTree(ShellKeyPath);
        _registry.DeleteTree(BackgroundShellKeyPath);
    }
}
