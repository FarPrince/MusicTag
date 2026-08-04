using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MusicTag.App.ViewModels;
using MusicTag.App.Views;
using MusicTag.App.Views.Dialogs;
using MusicTag.Core.Integration;
using MusicTag.Core.Models;
using MusicTag.Core.Services;
using MusicTag.Core.Settings;

namespace MusicTag.App.Services;

/// <summary>
/// ShowSettingsAsync's dependencies flow through here because SettingsWindow needs a
/// SettingsViewModel built fresh per open (see its own doc comment on why it's intentionally
/// not a DI singleton). Every modal dialog is awaited via Avalonia's Task-based
/// <c>Window.ShowDialog(Window owner)</c> — see IDialogService's own doc comment on why that's
/// async here where the WPF original was synchronous.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly ISettingsService _settingsService;
    private readonly IExplorerIntegrationService _explorerIntegrationService;
    private readonly IFilePickerService _filePickerService;
    private readonly IThemeService _themeService;
    private readonly ILyricsSearchService _lyricsSearchService;

    public DialogService(
        ISettingsService settingsService,
        IExplorerIntegrationService explorerIntegrationService,
        IFilePickerService filePickerService,
        IThemeService themeService,
        ILyricsSearchService lyricsSearchService)
    {
        _settingsService = settingsService;
        _explorerIntegrationService = explorerIntegrationService;
        _filePickerService = filePickerService;
        _themeService = themeService;
        _lyricsSearchService = lyricsSearchService;
    }

    private static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public async Task ShowSaveErrorsAsync(IReadOnlyList<(AudioFile File, Exception Error)> failures)
    {
        var dialog = new SaveErrorsDialog(failures);
        await ShowDialogAsync(dialog);
    }

    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        var dialog = new DiscardChangesDialog();
        await ShowDialogAsync(dialog);
        return dialog.Confirmed;
    }

    public async Task ShowRenameErrorAsync(string message)
    {
        var dialog = new RenameErrorDialog(message);
        await ShowDialogAsync(dialog);
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ErrorDialog(title, message);
        await ShowDialogAsync(dialog);
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        var dialog = new ErrorDialog(title, message);
        await ShowDialogAsync(dialog);
    }

    /// <summary>Builds a fresh SettingsViewModel per call (not a DI singleton — see the class
    /// doc comment) so every open reloads whatever is currently on disk/registered.</summary>
    public async Task ShowSettingsAsync()
    {
        var viewModel = new SettingsViewModel(_settingsService, _explorerIntegrationService, _filePickerService, _themeService, this);
        var window = new SettingsWindow(viewModel);
        await ShowDialogAsync(window);
    }

    /// <summary>Shows the static keyboard-shortcuts reference. Deliberately non-modal (<c>Show</c>,
    /// not <c>ShowDialog</c>) — unlike every other dialog here, this one has no state to collect
    /// or action to confirm, so there's no reason to block interacting with the main window
    /// while it's open. Owned by MainWindow so it closes if the app's main window does, but
    /// doesn't otherwise interfere with it.</summary>
    public void ShowShortcutsReference()
    {
        var window = new ShortcutsReferenceWindow();
        if (MainWindow is { } owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }

    public async Task ShowLyricsSearchDialogAsync(IReadOnlyList<string> directories)
    {
        var viewModel = new LyricsSearchDialogViewModel(_lyricsSearchService, directories);
        var dialog = new LyricsSearchDialog(viewModel);
        await ShowDialogAsync(dialog);
    }

    private static async Task ShowDialogAsync(Window dialog)
    {
        if (MainWindow is { } owner)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            // No main window yet (shouldn't happen in practice — every dialog is triggered by
            // user action against an already-open main window) — fall back to a non-modal show
            // rather than throwing, so a dialog can never silently fail to appear at all.
            dialog.Show();
        }
    }
}
