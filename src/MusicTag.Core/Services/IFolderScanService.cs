using MusicTag.Core.Models;

namespace MusicTag.Core.Services;

public interface IFolderScanService
{
    /// <summary>
    /// Enumerates and loads every supported audio file directly inside <paramref name="folderPath"/>
    /// (non-recursive — matches Mp3tag's default; recursive "include subfolders" would be a
    /// natural later addition, not required for MVP).
    /// </summary>
    IReadOnlyList<AudioFile> ScanFolder(string folderPath);
}
