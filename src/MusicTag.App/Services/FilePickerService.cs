using System.IO;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace MusicTag.App.Services;

/// <summary>
/// Folder/file picker backed by Avalonia's cross-platform <see cref="IStorageProvider"/> — the
/// one picker mechanism for every platform this app targets (native GTK/portal chooser on
/// Linux, native common-item dialog on Windows). Resolved from the app's current main window on
/// every call (rather than injected/cached at construction) since <see cref="FilePickerService"/>
/// is a DI singleton built before the main window exists.
/// </summary>
public sealed class FilePickerService : IFilePickerService
{
    private static readonly FilePickerFileType ImageFileType = new("Image files")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif"],
    };

    private static readonly FilePickerFileType PngFileType = new("PNG image") { Patterns = ["*.png"] };
    private static readonly FilePickerFileType JpegFileType = new("JPEG image") { Patterns = ["*.jpg"] };
    private static readonly FilePickerFileType BmpFileType = new("Bitmap image") { Patterns = ["*.bmp"] };
    private static readonly FilePickerFileType GifFileType = new("GIF image") { Patterns = ["*.gif"] };

    private static IStorageProvider? StorageProvider =>
        (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.StorageProvider;

    public async Task<string?> PickFolderAsync(string? initialDirectory = null)
    {
        var provider = StorageProvider;
        if (provider is null)
            return null;

        var options = new FolderPickerOpenOptions
        {
            Title = "Open Folder",
            AllowMultiple = false,
            SuggestedStartLocation = await ResolveStartLocationAsync(provider, initialDirectory),
        };

        var result = await provider.OpenFolderPickerAsync(options);
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    public async Task<string?> PickImageFileAsync()
    {
        var provider = StorageProvider;
        if (provider is null)
            return null;

        var result = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Album Art",
            AllowMultiple = false,
            FileTypeFilter = [ImageFileType, FilePickerFileTypes.All],
        });

        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    /// <summary><paramref name="suggestedFileName"/> already carries the correct extension for
    /// the art's sniffed format (see AlbumArtViewModel.Extract), so
    /// <see cref="FilePickerSaveOptions.DefaultExtension"/> is left unset rather than forcing
    /// one.</summary>
    public async Task<string?> PickSaveImageFileAsync(string suggestedFileName)
    {
        var provider = StorageProvider;
        if (provider is null)
            return null;

        var result = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Extract Album Art",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = [PngFileType, JpegFileType, BmpFileType, GifFileType, FilePickerFileTypes.All],
        });

        return result?.Path.LocalPath;
    }

    private static async Task<IStorageFolder?> ResolveStartLocationAsync(IStorageProvider provider, string? initialDirectory)
    {
        if (string.IsNullOrWhiteSpace(initialDirectory) || !Directory.Exists(initialDirectory))
            return null;

        try
        {
            return await provider.TryGetFolderFromPathAsync(initialDirectory);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
