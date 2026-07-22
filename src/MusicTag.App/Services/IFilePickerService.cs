namespace MusicTag.App.Services;

/// <summary>
/// Thin WPF-facing wrapper around folder-picking UI so view models stay unit-testable
/// (per the plan's IDialogService/IFilePickerService seam — IDialogService itself isn't
/// needed until later milestones introduce actual dialogs, e.g. RenameErrorDialog in M4).
/// </summary>
public interface IFilePickerService
{
    /// <summary>Shows a folder picker and returns the chosen path, or null if cancelled.</summary>
    string? PickFolder(string? initialDirectory = null);

    /// <summary>M6: shows a file picker filtered to common raster image formats (png/jpg/
    /// jpeg/bmp/gif), used by <see cref="MusicTag.App.ViewModels.AlbumArtViewModel"/>'s
    /// Replace action. Returns the chosen file's full path, or null if cancelled.</summary>
    string? PickImageFile();

    /// <summary>Shows a Save-As dialog for exporting embedded album art to a standalone image
    /// file, per user feedback ("provide an option to extract album art as an image"). Used by
    /// <see cref="MusicTag.App.ViewModels.AlbumArtViewModel"/>'s Extract action. Returns the
    /// chosen destination path, or null if cancelled.</summary>
    string? PickSaveImageFile(string suggestedFileName);
}
