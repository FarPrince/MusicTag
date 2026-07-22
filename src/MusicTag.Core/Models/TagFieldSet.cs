namespace MusicTag.Core.Models;

/// <summary>
/// Immutable snapshot of the 10 user-editable tag fields (filename and album art are
/// handled separately — see <see cref="AudioFile"/>). Being a <c>record</c> gives free
/// structural equality, which directly powers both <see cref="AudioFile.IsDirty"/> and
/// mixed-value detection across a batch selection without hand-written comparers.
/// </summary>
public sealed record TagFieldSet
{
    public string? Title { get; init; }
    public string? Album { get; init; }
    public string? Artist { get; init; }
    public string? AlbumArtist { get; init; }
    public string? Comment { get; init; }
    public string? Composer { get; init; }
    public string? Genre { get; init; }
    public int? Year { get; init; }
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
}
