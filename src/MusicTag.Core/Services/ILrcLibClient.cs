using MusicTag.Core.Models;

namespace MusicTag.Core.Services;

/// <summary>
/// Thin wrapper around LRCLib's (lrclib.net) public `/api/get` lookup — confirmed live against
/// the real API: `track_name`/`artist_name` are the only required parameters, `album_name` and
/// `duration` are optional refinements that improve match accuracy, and a miss returns HTTP 404
/// (mapped to a null return here) rather than an error payload.
/// </summary>
public interface ILrcLibClient
{
    /// <summary>Returns null when LRCLib has no matching track (HTTP 404) — a normal, expected
    /// outcome for an obscure or mistagged song, not an error. Network failures/timeouts are
    /// left to throw, so <see cref="ILyricsSearchService"/> can tell "not found" apart from
    /// "couldn't reach LRCLib" when tallying its per-file results.</summary>
    Task<LrcLibTrack?> GetAsync(
        string artistName, string trackName, string? albumName, int? durationSeconds, CancellationToken ct = default);
}
