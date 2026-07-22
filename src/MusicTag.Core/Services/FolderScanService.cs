using MusicTag.Core.Models;

namespace MusicTag.Core.Services;

public sealed class FolderScanService(IAudioFileService audioFileService) : IFolderScanService
{
    public IReadOnlyList<AudioFile> ScanFolder(string folderPath)
    {
        var results = new List<AudioFile>();

        foreach (var path in Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly))
        {
            if (!SupportedExtensions.IsSupported(Path.GetExtension(path)))
                continue;

            try
            {
                results.Add(audioFileService.Load(path));
            }
            catch (Exception)
            {
                // A single unreadable/corrupt file shouldn't prevent browsing the rest of
                // the folder. Later milestones may surface these as a warning list; for
                // M1 (read-only browsing) skipping silently is acceptable.
            }
        }

        return results;
    }
}
