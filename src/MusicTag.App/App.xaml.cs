using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MusicTag.App.Services;
using MusicTag.App.ViewModels;
using MusicTag.App.Views;
using MusicTag.Core.History;
using MusicTag.Core.Integration;
using MusicTag.Core.Services;
using MusicTag.Core.Settings;

namespace MusicTag.App;

/// <summary>
/// Startup, DI wiring, and theme application. M7 added SettingsService/ExplorerIntegrationService
/// DI registration and startup command-line arg parsing (Explorer-launch folder vs. the
/// configured default folder vs. an empty "Open Folder" prompt state), per plan section 7. M8
/// adds real System/Light/Dark theme switching (<see cref="IThemeService"/>) applied here from
/// the persisted setting, replacing the M7 placeholder's unconditional
/// <c>ApplicationThemeManager.ApplySystemTheme()</c> call.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Resolve a startup folder (if any) before showing the window, so the grid is already
        // populated on first paint rather than opening empty and then flashing to populated.
        // Awaited (rather than a blocking call) so the actual folder scan — which
        // LoadInitialFolder now runs via Task.Run — doesn't tie up the UI thread while it's
        // in progress; there's just no window shown yet for that to matter visibly. async
        // void is the standard pattern for a WPF lifecycle override with no async version.
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var settings = settingsService.Load();
        var startupFolder = ResolveStartupFolder(e.Args, settings);

        var mainWindowViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        if (startupFolder is not null)
        {
            await mainWindowViewModel.LoadInitialFolder(startupFolder);
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

        // Must register the window before ApplyTheme so "System" mode can start watching it
        // for live OS theme-change notifications, and so the Mica backdrop re-assert has a
        // window to target — see IThemeService/ThemeService's own doc comments.
        var themeService = _serviceProvider.GetRequiredService<IThemeService>();
        themeService.RegisterMainWindow(mainWindow);
        themeService.ApplyBackdrop(settings.Backdrop);
        themeService.ApplyTheme(settings.Theme);

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Per plan section 7: an Explorer-triggered launch (a real directory passed as a
    /// command-line arg — either the "Open with MusicTag" entry on a folder, which supplies
    /// %1, or the folder-background entry, which supplies %V) always wins over the configured
    /// default folder, since the user just explicitly asked to open that specific folder.
    /// Falls back to AppSettings.DefaultStartupFolder if it still exists on disk (it may have
    /// been renamed/deleted since it was configured); otherwise returns null, leaving
    /// MainWindowViewModel.CurrentFolderPath at its default null — the "empty, Open Folder
    /// prompt available" state the status bar and OpenFolderCommand already handle natively,
    /// with no extra empty-state UI needed.</summary>
    private static string? ResolveStartupFolder(string[] args, AppSettings settings)
    {
        var explorerLaunchFolder = args.FirstOrDefault(Directory.Exists);
        if (explorerLaunchFolder is not null)
        {
            return explorerLaunchFolder;
        }

        return settings.DefaultStartupFolder is { Length: > 0 } defaultFolder && Directory.Exists(defaultFolder)
            ? defaultFolder
            : null;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core (WPF-free) services.
        services.AddSingleton<IAudioFileService, AudioFileService>();
        services.AddSingleton<IFolderScanService, FolderScanService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IRegistryKeyWrapper, RegistryKeyWrapper>();
        services.AddSingleton<IExplorerIntegrationService, ExplorerIntegrationService>();

        // Session-only undo/redo history — a single instance shared for the app's lifetime
        // (registered as a singleton, not per-window/per-selection) since it must survive
        // across selection changes and folder-open/refresh operations within one session.
        services.AddSingleton<EditHistory>();

        // App (WPF-facing) services — thin wrappers so view models stay unit-testable.
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IThemeService, ThemeService>();

        // View models + views. SettingsViewModel/SettingsWindow are deliberately NOT
        // registered here — DialogService.ShowSettings builds a fresh instance per open (see
        // its own doc comment) rather than reusing a stale singleton.
        services.AddSingleton<EditPanelViewModel>();
        services.AddSingleton<AlbumArtViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
