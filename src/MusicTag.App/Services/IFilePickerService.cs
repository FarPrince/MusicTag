namespace MusicTag.App.Services;

/// <summary>
/// Thin Avalonia-facing wrapper around folder/file-picking UI so view models stay unit-testable.
/// Async (unlike the original WPF version's synchronous <c>Microsoft.Win32</c> dialogs) because
/// Avalonia's cross-platform <c>IStorageProvider</c> — the single picker mechanism for every
/// platform this app targets, backed by the native GTK/portal chooser on Linux and the native
/// common-item dialog on Windows — is Task-based end to end; there is no synchronous variant to
/// fall back to the way WPF had a WinForms fallback.
/// </summary>
public interface IFilePickerService
{
    /// <summary>Shows a folder picker and returns the chosen path, or null if cancelled.</summary>
    Task<string?> PickFolderAsync(string? initialDirectory = null);

    /// <summary>Shows a file picker filtered to common raster image formats (png/jpg/jpeg/bmp/
    /// gif), used by <see cref="MusicTag.App.ViewModels.AlbumArtViewModel"/>'s Replace action.
    /// Returns the chosen file's full path, or null if cancelled.</summary>
    Task<string?> PickImageFileAsync();

    /// <summary>Shows a Save-As dialog for exporting embedded album art to a standalone image
    /// file. Used by <see cref="MusicTag.App.ViewModels.AlbumArtViewModel"/>'s Extract action.
    /// Returns the chosen destination path, or null if cancelled.</summary>
    Task<string?> PickSaveImageFileAsync(string suggestedFileName);
}
