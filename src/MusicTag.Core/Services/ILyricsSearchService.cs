namespace MusicTag.Core.Services;

/// <summary>Per-file outcome reported as each file finishes processing — drives both the
/// popup's live progress bar (via <see cref="LyricsFileResult.Current"/>/<see cref="LyricsFileResult.Total"/>)
/// and its per-file result list (via <see cref="LyricsFileResult.FileName"/>/<see cref="LyricsFileResult.Outcome"/>).
/// Files run concurrently (see <see cref="LyricsSearchService"/>), so Current is simply "how many
/// files have finished so far," not this file's position in the original enumeration order.</summary>
public enum LyricsFileOutcome
{
    Downloaded,
    AlreadyHadLyrics,
    NoMatch,
    Error,
}

public sealed record LyricsFileResult(string FileName, LyricsFileOutcome Outcome, int Current, int Total);

/// <summary>End-of-search tally shown to the user, per file examined across every configured
/// directory (recursively): downloaded a new synced .lrc, already had one (never overwritten —
/// see <see cref="LyricsSearchService"/>), no usable match on LRCLib (including untagged files
/// whose filename didn't follow "Artist - Title" either, and matches with only plain/instrumental
/// lyrics), or a hard error (network/IO/tag-read failure).</summary>
public sealed record LyricsSearchResult(int Downloaded, int AlreadyHadLyrics, int NoMatch, int Errors);

/// <summary>
/// Orchestrates the "find synced lyrics for my library" feature end to end: walks the
/// user-configured search directories, skips anything that already has a sidecar .lrc, resolves
/// artist/title from tags (falling back to filename parsing), queries LRCLib, and writes a
/// matching .lrc file next to each song that gets a synced-lyrics hit. Only files with a
/// supported audio extension (<see cref="SupportedExtensions"/>) are ever considered — everything
/// else encountered while walking the directories is skipped before any tag read or network call.
/// </summary>
public interface ILyricsSearchService
{
    Task<LyricsSearchResult> SearchAsync(
        IReadOnlyList<string> directories, IProgress<LyricsFileResult>? progress = null, CancellationToken ct = default);
}
