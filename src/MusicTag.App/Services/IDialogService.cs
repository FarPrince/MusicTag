using MusicTag.Core.Models;

namespace MusicTag.App.Services;

/// <summary>
/// Thin WPF-facing wrapper around dialogs so view models stay unit-testable (per the plan's
/// IDialogService/IFilePickerService seam). M2 added the save-errors report; M3 added the
/// discard-confirmation dialog; M4 adds the rename-error dialog; later milestones add
/// AboutDialog, etc. — this interface grows one method per dialog rather than becoming a
/// generic "ShowDialog(object)" so each caller's intent stays explicit.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows an end-of-save error report listing every file that failed to save
    /// and why. Only called when <c>BatchSaveResult.Failed</c> is non-empty.</summary>
    void ShowSaveErrors(IReadOnlyList<(AudioFile File, Exception Error)> failures);

    /// <summary>Shows a discard-confirmation dialog, called before Refresh/Open Folder
    /// proceeds when at least one file in the currently open folder is dirty (per plan
    /// section 4). Returns true if the user chose to discard and continue, false to cancel
    /// and leave the current folder/edits exactly as they were.</summary>
    bool ConfirmDiscardChanges();

    /// <summary>Shows a rename-failure error dialog with <paramref name="message"/> (from the
    /// exception EditHistory.TryExecute/TryUndo/TryRedo surfaced for a RenameCommand — e.g. a
    /// filename collision, a locked file, or a since-vacated/occupied name on undo/redo). Per
    /// plan section 4, the caller never needs to separately "revert" the UI — a failed Rename
    /// call never mutates AudioFile.FileName, so the grid already shows the unchanged name.</summary>
    void ShowRenameError(string message);

    /// <summary>M7: shows the modal Settings window (default startup folder, theme choice,
    /// Explorer-integration register/unregister toggle). A fresh SettingsViewModel is built for
    /// each call (see DialogService) rather than a DI singleton, so every open reloads whatever
    /// is currently on disk instead of showing stale in-memory state from a previous open.</summary>
    void ShowSettings();

    /// <summary>M8: shows the static keyboard-shortcuts reference (Help menu → "Keyboard
    /// Shortcuts"), non-modal — see DialogService for why.</summary>
    void ShowShortcutsReference();

    /// <summary>Generic "an operation failed" report — <paramref name="title"/> names what was
    /// being attempted (e.g. "Couldn't Open Folder", "Explorer Integration",
    /// "Couldn't Save Settings") and <paramref name="message"/> is the underlying exception's
    /// message. Shared by every unguarded-operation error path that isn't specific enough to
    /// warrant its own dialog (contrast <see cref="ShowRenameError"/>, which is tied to the
    /// specific TryExecute/TryUndo/TryRedo rename flow).</summary>
    void ShowError(string title, string message);

    /// <summary>Generic "an operation finished, here's the result" report — same title+message
    /// shape as <see cref="ShowError"/> (and the same underlying dialog), but for outcomes that
    /// aren't failures, e.g. the lyrics-search button's "nothing configured yet" guard.</summary>
    void ShowInfo(string title, string message);

    /// <summary>Shows the "Search Lyrics" progress popup for the given (already validated
    /// non-empty) search directories — live progress bar, per-file result list, and a
    /// Cancel/Close button. Unlike every other dialog here, this one runs the actual search
    /// itself (via <see cref="ViewModels.LyricsSearchDialogViewModel"/>) rather than just
    /// displaying a result the caller already has, since the whole point is showing progress
    /// as it happens.</summary>
    void ShowLyricsSearchDialog(IReadOnlyList<string> directories);
}
