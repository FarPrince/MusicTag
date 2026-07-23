namespace MusicTag.Core.Services;

/// <summary>
/// Fallback used by <see cref="LyricsSearchService"/> when a file's Artist/Title tags are
/// missing or blank — parses the common "Artist - Title" filename convention so those files
/// still get a lyrics lookup attempt instead of being skipped outright. Deliberately narrow
/// (a single " - " split) rather than a fuzzier heuristic: a wrong guess here would send a
/// bogus query to LRCLib and silently mismatch the downloaded lyrics to the wrong song, which
/// is worse than just skipping a file that doesn't follow the convention.
/// </summary>
public static class LyricsFilenameParser
{
    public static bool TryParseArtistAndTitle(string fileNameWithoutExtension, out string artist, out string title)
    {
        var separatorIndex = fileNameWithoutExtension.IndexOf(" - ", StringComparison.Ordinal);

        if (separatorIndex > 0)
        {
            var candidateArtist = fileNameWithoutExtension[..separatorIndex].Trim();
            var candidateTitle = fileNameWithoutExtension[(separatorIndex + 3)..].Trim();

            if (candidateArtist.Length > 0 && candidateTitle.Length > 0)
            {
                artist = candidateArtist;
                title = candidateTitle;
                return true;
            }
        }

        artist = string.Empty;
        title = string.Empty;
        return false;
    }
}
