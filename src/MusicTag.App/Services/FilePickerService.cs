using System.IO;
using Microsoft.Win32;

namespace MusicTag.App.Services;

/// <summary>
/// Folder picker. Primary path is the newer .NET 8 WPF-native
/// <see cref="Microsoft.Win32.OpenFolderDialog"/> (a modern common-item-dialog folder
/// picker with no WinForms dependency); if that throws for any reason (this was flagged as
/// an open risk in the plan — it's a newer API), falls back to the WinForms
/// <see cref="System.Windows.Forms.FolderBrowserDialog"/>, which has been stable for
/// decades. Both are exercised through this one seam so a future milestone (or a bug
/// report from a specific Windows version) only needs to change this file.
/// </summary>
public sealed class FilePickerService : IFilePickerService
{
    public string? PickFolder(string? initialDirectory = null)
    {
        try
        {
            return PickFolderWithOpenFolderDialog(initialDirectory);
        }
        catch (Exception)
        {
            return PickFolderWithWinFormsFallback(initialDirectory);
        }
    }

    private static string? PickFolderWithOpenFolderDialog(string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Open Folder",
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private static string? PickFolderWithWinFormsFallback(string? initialDirectory)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Open Folder",
            UseDescriptionForTitle = true,
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.SelectedPath = initialDirectory;
        }

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    /// <summary>
    /// M6: uses the same WPF-native <see cref="Microsoft.Win32"/> dialog family as
    /// <see cref="PickFolder"/> (fully-qualified here — MusicTag.App also references
    /// WinForms for the folder-picker fallback above, and
    /// <c>System.Windows.Forms.OpenFileDialog</c> shares the bare name "OpenFileDialog",
    /// so an unqualified reference would be ambiguous). No WinForms fallback is needed
    /// here (unlike the folder picker) since Microsoft.Win32.OpenFileDialog has been
    /// stable since .NET Framework — only the folder-picker equivalent was flagged as a
    /// newer, unverified API in the plan (risk #4).
    /// </summary>
    public string? PickImageFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Album Art",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
            Multiselect = false,
            CheckFileExists = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>Per user feedback ("provide an option to extract album art as an image") —
    /// same <see cref="Microsoft.Win32.SaveFileDialog"/> family as the rest of this class.
    /// <paramref name="suggestedFileName"/> already carries the correct extension for the
    /// art's sniffed format (see AlbumArtViewModel.Extract), so AddExtension is left at its
    /// default rather than forcing one.</summary>
    public string? PickSaveImageFile(string suggestedFileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Extract Album Art",
            FileName = suggestedFileName,
            Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg|Bitmap image (*.bmp)|*.bmp|GIF image (*.gif)|*.gif|All files (*.*)|*.*",
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
