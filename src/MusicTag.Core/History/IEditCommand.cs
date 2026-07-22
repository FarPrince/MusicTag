namespace MusicTag.Core.History;

/// <summary>
/// A single undoable unit of work. Per plan section 4, tag-field and album-art commands are
/// pure in-memory (<see cref="FieldEditCommand"/>, <see cref="AlbumArtEditCommand"/>,
/// <see cref="CompositeEditCommand"/> — none of these can fail) — disk writes only happen at
/// explicit Save. The one command type that performs real disk I/O in <see cref="Do"/>/
/// <see cref="Undo"/> (<see cref="RenameCommand"/>, since filenames are committed immediately
/// rather than staged) can legitimately throw, which is why it's always driven through
/// EditHistory's fallible TryExecute/TryUndo/TryRedo rather than the plain Execute.
/// </summary>
public interface IEditCommand
{
    /// <summary>Human-readable summary shown in the status bar (top-of-undo-stack) and
    /// usable in future error dialogs, e.g. "Set Artist on 3 files", "Rename track03.mp3 →
    /// 03 - Song.mp3".</summary>
    string Description { get; }

    void Do();

    void Undo();
}
