namespace MusicTag.Core.Models;

/// <summary>
/// The subset of LRCLib's (lrclib.net) `/api/get` response this app actually needs — see
/// <see cref="Services.ILrcLibClient"/>. <see cref="Instrumental"/> tracks and results with no
/// <see cref="SyncedLyrics"/> are both treated as "nothing to download" by
/// <see cref="Services.ILyricsSearchService"/>, per the user's request to only ever write
/// time-synced `.lrc` files, never plain lyrics.
/// </summary>
public sealed record LrcLibTrack(string? SyncedLyrics, string? PlainLyrics, bool Instrumental);
