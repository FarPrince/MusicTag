using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicTag.App.Services;
using MusicTag.Core.Integration;
using MusicTag.Core.Models;
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
    private readonly IDialogService _dialogService;

    public SettingsViewModel(
        ISettingsService settingsService,
        IExplorerIntegrationService explorerIntegrationService,
        IFilePickerService filePickerService,
        IThemeService themeService,
        IDialogService dialogService)
    {
        _settingsService = settingsService;
        _explorerIntegrationService = explorerIntegrationService;
        _filePickerService = filePickerService;
        _themeService = themeService;
        _dialogService = dialogService;

        var settings = _settingsService.Load();
        defaultStartupFolder = settings.DefaultStartupFolder;
        theme = settings.Theme;
        backdrop = settings.Backdrop;

        titleSeparatorEnabled = settings.SeparatorNormalizationFields.Contains("Title");
        albumSeparatorEnabled = settings.SeparatorNormalizationFields.Contains("Album");
        artistSeparatorEnabled = settings.SeparatorNormalizationFields.Contains("Artist");
        albumArtistSeparatorEnabled = settings.SeparatorNormalizationFields.Contains("AlbumArtist");
        commentSeparatorEnabled = settings.SeparatorNormalizationFields.Contains("Comment");
        composerSeparatorEnabled = settings.SeparatorNormalizationFields.Contains("Composer");
        genreSeparatorEnabled = settings.SeparatorNormalizationFields.Contains("Genre");

        LyricsSearchDirectories = new ObservableCollection<string>(settings.LyricsSearchDirectories);

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

    /// <summary>Backs the "Separator Normalization" section's per-field checkboxes — one bool
    /// property per <see cref="SeparatorNormalization.FieldNames"/> entry, same one-property-per-
    /// toggle convention MainWindowViewModel already uses for its grid column-chooser. Title/Album/
    /// Comment default unchecked (see AppSettings.SeparatorNormalizationFields doc comment).</summary>
    [ObservableProperty]
    private bool titleSeparatorEnabled;

    [ObservableProperty]
    private bool albumSeparatorEnabled;

    [ObservableProperty]
    private bool artistSeparatorEnabled;

    [ObservableProperty]
    private bool albumArtistSeparatorEnabled;

    [ObservableProperty]
    private bool commentSeparatorEnabled;

    [ObservableProperty]
    private bool composerSeparatorEnabled;

    [ObservableProperty]
    private bool genreSeparatorEnabled;

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

    [ObservableProperty]
    private string backdrop;

    /// <summary>Backs the Settings window's backdrop ComboBox — per user request, moved here from
    /// the toolbar's old quick-toggle button (MainWindowViewModel.ToggleBackdrop). "Acrylic" |
    /// "Mica", applied live via the same "act immediately, persist on Save" pattern as
    /// <see cref="OnThemeChanged"/> above.</summary>
    public IReadOnlyList<string> BackdropOptions { get; } = ["Acrylic", "Mica"];

    partial void OnBackdropChanged(string value) => _themeService.ApplyBackdrop(value);

    /// <summary>Backs the "Lyrics (LRCLib)" section's directory list — populated from
    /// <see cref="Core.Settings.AppSettings.LyricsSearchDirectories"/> in the constructor,
    /// mutated by <see cref="AddLyricsSearchDirectory"/>/<see cref="RemoveLyricsSearchDirectory"/>,
    /// and only persisted back to disk on <see cref="Save"/> — same "live in-memory list, commit
    /// on Save" treatment as every other field in this window.</summary>
    public ObservableCollection<string> LyricsSearchDirectories { get; }

    [ObservableProperty]
    private string? selectedLyricsSearchDirectory;

    [RelayCommand]
    private void AddLyricsSearchDirectory()
    {
        var folder = _filePickerService.PickFolder(null);
        if (folder is not null && !LyricsSearchDirectories.Contains(folder))
        {
            LyricsSearchDirectories.Add(folder);
        }
    }

    private bool CanRemoveLyricsSearchDirectory() => SelectedLyricsSearchDirectory is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveLyricsSearchDirectory))]
    private void RemoveLyricsSearchDirectory()
    {
        if (SelectedLyricsSearchDirectory is not null)
        {
            LyricsSearchDirectories.Remove(SelectedLyricsSearchDirectory);
        }
    }

    partial void OnSelectedLyricsSearchDirectoryChanged(string? value) => RemoveLyricsSearchDirectoryCommand.NotifyCanExecuteChanged();

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
    /// for the user to also click Save. Register()/Unregister() can throw (restricted HKCU
    /// permissions/Group Policy, or a null Environment.ProcessPath) and there's no global
    /// unhandled-exception handler, so an unguarded failure here would crash the whole app over
    /// what should be a recoverable, reportable error — caught and shown instead.
    /// ExplorerIntegrationRegistered is refreshed from the live registry state either way, so a
    /// partial failure (some keys written, some not) is reflected as accurately as
    /// IsRegistered() can tell, rather than assumed to have fully succeeded or fully failed.</summary>
    [RelayCommand]
    private void ToggleExplorerIntegration()
    {
        try
        {
            if (ExplorerIntegrationRegistered)
            {
                _explorerIntegrationService.Unregister();
            }
            else
            {
                _explorerIntegrationService.Register();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _dialogService.ShowError("Explorer Integration", ex.Message);
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
        settings.Backdrop = Backdrop;
        settings.ExplorerIntegrationRegistered = ExplorerIntegrationRegistered;

        var separatorFields = new HashSet<string>();
        if (TitleSeparatorEnabled) separatorFields.Add("Title");
        if (AlbumSeparatorEnabled) separatorFields.Add("Album");
        if (ArtistSeparatorEnabled) separatorFields.Add("Artist");
        if (AlbumArtistSeparatorEnabled) separatorFields.Add("AlbumArtist");
        if (CommentSeparatorEnabled) separatorFields.Add("Comment");
        if (ComposerSeparatorEnabled) separatorFields.Add("Composer");
        if (GenreSeparatorEnabled) separatorFields.Add("Genre");
        settings.SeparatorNormalizationFields = separatorFields;
        settings.LyricsSearchDirectories = [.. LyricsSearchDirectories];

        try
        {
            _settingsService.Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Matches MainWindow.xaml.cs's OnClosing guard around the identical call — a
            // settings-save failure (locked file, full disk) must not crash the app over
            // what's otherwise just a "couldn't persist this" report.
            _dialogService.ShowError("Couldn't Save Settings", ex.Message);
            return;
        }

        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, EventArgs.Empty);
}
