using MusicTag.Core.Models;

namespace MusicTag.Core.History;

/// <summary>
/// Captures one <see cref="AudioFile"/>'s before/after <see cref="TagFieldSet"/> as a single
/// undoable unit. Pure in-memory — <see cref="Do"/>/<see cref="Undo"/> just swap which
/// snapshot is assigned to <see cref="AudioFile.PendingFields"/>, which cannot fail, so this
/// is always driven through <see cref="EditHistory.Execute"/> rather than the fallible
/// Try* API (that's reserved for RenameCommand's real disk I/O — see plan section 4).
/// </summary>
public sealed class FieldEditCommand : IEditCommand
{
    private readonly AudioFile _file;
    private readonly TagFieldSet _before;
    private readonly TagFieldSet _after;

    public FieldEditCommand(AudioFile file, TagFieldSet before, TagFieldSet after, string description)
    {
        _file = file;
        _before = before;
        _after = after;
        Description = description;
    }

    public string Description { get; }

    public void Do() => _file.PendingFields = _after;

    public void Undo() => _file.PendingFields = _before;
}
