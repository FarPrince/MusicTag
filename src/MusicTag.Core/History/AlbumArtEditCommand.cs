using MusicTag.Core.Models;

namespace MusicTag.Core.History;

/// <summary>
/// Same shape as <see cref="FieldEditCommand"/>, but over <see cref="AudioFile.PendingAlbumArt"/>.
/// The album-art editing UI itself (replace/remove/paste) is M6 scope, but the command class
/// can exist now — it's pure in-memory (no disk I/O) like every other non-rename command, per
/// plan section 4, so there's nothing about it that depends on the UI that will eventually
/// construct it.
/// </summary>
public sealed class AlbumArtEditCommand : IEditCommand
{
    private readonly AudioFile _file;
    private readonly AlbumArtEdit _before;
    private readonly AlbumArtEdit _after;

    public AlbumArtEditCommand(AudioFile file, AlbumArtEdit before, AlbumArtEdit after, string description)
    {
        _file = file;
        _before = before;
        _after = after;
        Description = description;
    }

    public string Description { get; }

    public void Do() => _file.PendingAlbumArt = _after;

    public void Undo() => _file.PendingAlbumArt = _before;
}
