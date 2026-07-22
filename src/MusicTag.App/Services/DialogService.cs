using MusicTag.App.ViewModels;
using MusicTag.App.Views;
using MusicTag.App.Views.Dialogs;
using MusicTag.Core.Integration;
using MusicTag.Core.Models;
using MusicTag.Core.Settings;

namespace MusicTag.App.Services;

/// <summary>
/// M7 adds ShowSettings and, with it, this class's first constructor dependencies — previously
/// every dialog here was simple enough to be built with no injected state at all. SettingsWindow
/// needs a SettingsViewModel built fresh per open (see ShowSettings' own doc comment on why it's
/// intentionally not a DI singleton), so its three dependencies flow through here instead.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly ISettingsService _settingsService;
    private readonly IExplorerIntegrationService _explorerIntegrationService;
    private readonly IFilePickerService _filePickerService;
    private readonly IThemeService _themeService;

    public DialogService(
        ISettingsService settingsService,
        IExplorerIntegrationService explorerIntegrationService,
        IFilePickerService filePickerService,
        IThemeService themeService)
    {
        _settingsService = settingsService;
        _explorerIntegrationService = explorerIntegrationService;
        _filePickerService = filePickerService;
        _themeService = themeService;
    }

    public void ShowSaveErrors(IReadOnlyList<(AudioFile File, Exception Error)> failures)
    {
        var dialog = new SaveErrorsDialog(failures)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };

        dialog.ShowDialog();
    }

    public bool ConfirmDiscardChanges()
    {
        var dialog = new DiscardChangesDialog
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };

        dialog.ShowDialog();
        return dialog.Confirmed;
    }

    public void ShowRenameError(string message)
    {
        var dialog = new RenameErrorDialog(message)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };

        dialog.ShowDialog();
    }

    /// <summary>Builds a fresh SettingsViewModel per call (not a DI singleton — see the class
    /// doc comment) so every open reloads whatever is currently on disk/in the registry.</summary>
    public void ShowSettings()
    {
        var viewModel = new SettingsViewModel(_settingsService, _explorerIntegrationService, _filePickerService, _themeService);
        var window = new SettingsWindow(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };

        window.ShowDialog();
    }

    /// <summary>M8: shows the static keyboard-shortcuts reference (Help menu → "Keyboard
    /// Shortcuts"). Deliberately non-modal (<c>Show</c>, not <c>ShowDialog</c>) — unlike every
    /// other dialog here, this one has no state to collect or action to confirm, so there's no
    /// reason to block interacting with the main window while it's open; a user plausibly wants
    /// to glance at it while trying a shortcut. Owned by MainWindow so it closes if the app's
    /// main window does, but doesn't otherwise interfere with it.</summary>
    public void ShowShortcutsReference()
    {
        var window = new ShortcutsReferenceWindow
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };

        window.Show();
    }
}
