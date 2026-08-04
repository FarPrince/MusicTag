using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
/// DI wiring and theme application. The file-manager-integration CLI flags (Windows Explorer /
/// Linux .desktop+Nautilus-script registration) are handled entirely in <see cref="Program"/>,
/// before Avalonia's classic desktop lifetime — and therefore this class — ever starts, so
/// unlike the WPF original's App.OnStartup, there is no early-return branch here for them.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Resolve a startup folder (if any) before showing the window, so the grid is
            // already populated on first paint rather than opening empty and then flashing to
            // populated. The actual folder scan runs via Task.Run inside LoadInitialFolder, so
            // this await doesn't block the UI thread while it's in progress — there's just no
            // window shown yet for that to matter visibly.
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            var settings = settingsService.Load();
            var startupFolder = ResolveStartupFolder(desktop.Args ?? [], settings);

            var mainWindowViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            if (startupFolder is not null)
            {
                await mainWindowViewModel.LoadInitialFolder(startupFolder);
            }

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;

            // Must register the window before ApplyTheme/ApplyBackdrop so backdrop application
            // has a window to target — see ThemeService's own doc comment.
            var themeService = _serviceProvider.GetRequiredService<IThemeService>();
            themeService.RegisterMainWindow(mainWindow);
            themeService.ApplyBackdrop(settings.Backdrop);
            themeService.ApplyTheme(settings.Theme);

            // Avalonia's classic desktop lifetime only auto-shows whatever MainWindow was set
            // to at the exact moment OnFrameworkInitializationCompleted first returns — since
            // this method suspends at the LoadInitialFolder await above whenever a startup
            // folder is resolved, that moment can occur before MainWindow is even assigned,
            // silently leaving the app running with no window ever shown. Real bug caught by
            // actually launching the app (Xvfb + screenshot) rather than just reviewing the
            // code: the no-startup-folder path (nothing to await) worked fine and looked
            // correct by inspection, while the startup-folder path produced a live, silent,
            // windowless process. Showing explicitly here is unconditionally correct regardless
            // of timing.
            mainWindow.Show();

            desktop.ShutdownRequested += (_, _) => _serviceProvider?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>An Explorer/file-manager-triggered launch (a real directory passed as a command-
    /// line arg — either the "Open with MusicTag" entry on a folder, which supplies the clicked
    /// folder's path, or the folder-background entry, which supplies the currently viewed
    /// folder) always wins over the configured default folder, since the user just explicitly
    /// asked to open that specific folder. Falls back to AppSettings.DefaultStartupFolder if it
    /// still exists on disk (it may have been renamed/deleted since it was configured);
    /// otherwise returns null, leaving MainWindowViewModel.CurrentFolderPath at its default
    /// null — the "empty, Open Folder prompt available" state the status bar and
    /// OpenFolderCommand already handle natively, with no extra empty-state UI needed.</summary>
    private static string? ResolveStartupFolder(IReadOnlyList<string> args, AppSettings settings)
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
        // Core (UI-framework-free) services.
        services.AddSingleton<IAudioFileService, AudioFileService>();
        services.AddSingleton<IFolderScanService, FolderScanService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILrcLibClient, LrcLibClient>();
        services.AddSingleton<ILyricsSearchService, LyricsSearchService>();

        // File-manager integration: Windows Explorer (HKCU registry) vs. Linux (.desktop +
        // Nautilus script) — see IExplorerIntegrationService's own doc comment.
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

        // Session-only undo/redo history — a single instance shared for the app's lifetime
        // (registered as a singleton, not per-window/per-selection) since it must survive
        // across selection changes and folder-open/refresh operations within one session.
        services.AddSingleton<EditHistory>();

        // App (Avalonia-facing) services — thin wrappers so view models stay unit-testable.
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IThemeService, ThemeService>();

        // View models + views. SettingsViewModel/SettingsWindow are deliberately NOT registered
        // here — DialogService.ShowSettingsAsync builds a fresh instance per open (see its own
        // doc comment) rather than reusing a stale singleton.
        services.AddSingleton<EditPanelViewModel>();
        services.AddSingleton<AlbumArtViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
