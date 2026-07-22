namespace MusicTag.Core.Models;

/// <summary>
/// Album art is binary and needs a tri-state action (as opposed to a plain nullable
/// byte array) so "the user explicitly removed the cover" is distinguishable from
/// "the user never touched album art at all".
/// </summary>
public enum AlbumArtAction
{
    Unchanged,
    Removed,
    Replaced,
}

public sealed record AlbumArtEdit(AlbumArtAction Action, byte[]? NewImageBytes)
{
    public static readonly AlbumArtEdit Unchanged = new(AlbumArtAction.Unchanged, null);
}
