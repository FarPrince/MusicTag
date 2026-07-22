using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicTag.App.Services;
using MusicTag.Core.Integration;
using MusicTag.Core.Settings;

namespace MusicTag.App.ViewModels;

/// <summary>
/// M7/M8 scope, per plan sections 6-8: default startup folder, theme choice, and the
/// Explorer-integration register/unregister toggle. Deliberately NOT a DI singleton (see
/// DialogService.ShowSettings, which builds one of these per call) — each open of the Settings
/// window should reflect whatever is currently on disk/in the registry, not a stale snapshot
/// from a previous open earlier in the session.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IExplorerIntegrationService _explorerIntegrationService;
    private readonly IFilePickerService _filePickerService;
    private readonly IThemeService _themeService;

    public SettingsViewModel(
        ISettingsService settingsService,
        IExplorerIntegrationService explorerIntegrationService,
        IFilePickerService filePickerService,
        IThemeService themeService)
    {
        _settingsService = settingsService;
        _explorerIntegrationService = explorerIntegrationService;
        _filePickerService = filePickerService;
        _themeService = themeService;

        var settings = _settingsService.Load();
        defaultStartupFolder = settings.DefaultStartupFolder;
        theme = settings.Theme;

        // The live registry state is authoritative, not the persisted
        // AppSettings.ExplorerIntegrationRegistered flag — a registry key added/removed
        // outside the app (or a stale settings.json) must not lie to the user about whether
        // the integration is actually active right now.
        explorerIntegrationRegistered = _explorerIntegrationService.IsRegistered();
    }

    /// <summary>Raised when Save or Cancel completes, so SettingsWindow's code-behind can close
    /// itself — kept as a plain event rather than reaching into WPF's Window type from this
    /// (intentionally WPF-agnostic-in-spirit, though it lives in the App project) view model.</summary>
    public event EventHandler? RequestClose;

    [ObservableProperty]
    private string? defaultStartupFolder;

    [ObservableProperty]
    private string theme;

    [ObservableProperty]
    private bool explorerIntegrationRegistered;

    /// <summary>Backs the Settings window's theme ComboBox. "System" | "Light" | "Dark" per
    /// plan section 6 — order matches the AppSettings.Theme doc comment.</summary>
    public IReadOnlyList<string> ThemeOptions { get; } = ["System", "Light", "Dark"];

    /// <summary>M8: applies the newly-picked theme live, immediately — not deferred to Save.
    /// Mirrors <see cref="ToggleExplorerIntegration"/>'s existing "acts on the real world right
    /// away, independent of Save/Cancel" precedent: if the user picks a theme here and then
    /// clicks Cancel, the live visual switch is not rolled back (same as an Explorer-integration
    /// toggle isn't undone by Cancel) — only whether it's *persisted* to settings.json waits for
    /// Save. Never fires during construction: CommunityToolkit's generated property setter
    /// (which this partial hooks) is only invoked by an actual assignment through the property,
    /// and the constructor above sets the backing field directly.</summary>
    partial void OnThemeChanged(string value) => _themeService.ApplyTheme(value);

    [RelayCommand]
    private void BrowseDefaultFolder()
    {
        var folder = _filePickerService.PickFolder(DefaultStartupFolder);
        if (folder is not null)
        {
            DefaultStartupFolder = folder;
        }
    }

    /// <summary>Registers or unregisters immediately (not deferred to Save) — this is a live
    /// registry action, not an in-memory pending edit, so its own success/failure is reflected
    /// right away via <see cref="IExplorerIntegrationService.IsRegistered"/> rather than waiting
    /// for the user to also click Save.</summary>
    [RelayCommand]
    private void ToggleExplorerIntegration()
    {
        if (ExplorerIntegrationRegistered)
        {
            _explorerIntegrationService.Unregister();
        }
        else
        {
            _explorerIntegrationService.Register();
        }

        ExplorerIntegrationRegistered = _explorerIntegrationService.IsRegistered();
    }

    [RelayCommand]
    private void Save()
    {
        // Reload-then-mutate rather than serializing this view model's own snapshot straight
        // out, so a setting this window never exposed (none currently, but a future one) isn't
        // silently clobbered back to whatever it happened to be when this window opened.
        var settings = _settingsService.Load();
        settings.DefaultStartupFolder = DefaultStartupFolder;
        settings.Theme = Theme;
        settings.ExplorerIntegrationRegistered = ExplorerIntegrationRegistered;
        _settingsService.Save(settings);

        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, EventArgs.Empty);
}
