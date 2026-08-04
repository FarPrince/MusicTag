using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using MusicTag.Core.Integration;

namespace MusicTag.App;

/// <summary>
/// Entry point. Avalonia's <c>AppBuilder</c>/classic-desktop-lifetime replaces WPF's
/// <c>App.OnStartup</c> override as the place a .NET GUI app's <c>Main</c> normally lives — kept
/// as a genuinely separate <c>Program</c> class (rather than folded into <see cref="App"/>, which
/// WPF's generated <c>Main</c> effectively was) because the file-manager-integration CLI flags
/// (<c>--register-file-manager</c>/<c>--unregister-file-manager</c>, used by
/// installer/fedora/musictag.spec's post-install/pre-uninstall scriptlets — see
/// LinuxFileManagerIntegrationService) need to run and exit before Avalonia's windowing
/// subsystem ever spins up, matching the original WPF App.xaml.cs's own early-return in
/// OnStartup before any window is shown.
/// </summary>
public static class Program
{
    private const string RegisterFileManagerArg = "--register-file-manager";
    private const string UnregisterFileManagerArg = "--unregister-file-manager";

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains(RegisterFileManagerArg) || args.Contains(UnregisterFileManagerArg))
        {
            return RunFileManagerIntegrationCliMode(args.Contains(RegisterFileManagerArg));
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // Referenced by the Avalonia XAML previewer/designer (via reflection, looking for a public
    // static BuildAvaloniaApp() on the app's entry-point class) — safe to keep public even
    // though Main is the only other caller.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>Registers or unregisters the file-manager integration (Windows Explorer's
    /// right-click menu, or the Linux .desktop/Nautilus-script pair — see
    /// <see cref="IExplorerIntegrationService"/>'s doc comment) and exits immediately, with no
    /// window and no DI-registered app services beyond what this one operation needs. Exit code
    /// is 0 on success, 1 on failure, so the installer's log can tell whether the step actually
    /// worked without parsing anything else.</summary>
    private static int RunFileManagerIntegrationCliMode(bool register)
    {
        var services = new ServiceCollection();
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IRegistryKeyWrapper, RegistryKeyWrapper>();
            services.AddSingleton<IExplorerIntegrationService, ExplorerIntegrationService>();
        }
        else
        {
            services.AddSingleton<ILinuxDesktopFileWriter, LinuxDesktopFileWriter>();
            services.AddSingleton<IExplorerIntegrationService, LinuxFileManagerIntegrationService>();
        }

        using var provider = services.BuildServiceProvider();
        var fileManagerIntegrationService = provider.GetRequiredService<IExplorerIntegrationService>();

        try
        {
            if (register)
            {
                fileManagerIntegrationService.Register();
            }
            else
            {
                fileManagerIntegrationService.Unregister();
            }

            return 0;
        }
        catch (Exception)
        {
            // Best-effort, matching SettingsViewModel.ToggleExplorerIntegration's own tolerance
            // for a restricted environment — the installer/uninstaller should still be able to
            // complete even if this particular step couldn't.
            return 1;
        }
    }
}
