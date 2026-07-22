using MusicTag.Core.Models;
using MusicTag.Core.Services;

namespace MusicTag.Core.History;

/// <summary>
/// The one command type that performs real disk I/O in <see cref="Do"/>/<see cref="Undo"/>,
/// per plan section 4 — a direct consequence of the user's choice to rename immediately on
/// disk rather than defer to Save. <see cref="Do"/>/<see cref="Undo"/> can legitimately throw
/// (name collision, file lock, permissions), which is why this must always be driven through
/// <see cref="EditHistory.TryExecute"/>/<see cref="EditHistory.TryUndo"/>/
/// <see cref="EditHistory.TryRedo"/> rather than the plain <see cref="EditHistory.Execute"/>
/// used for the pure in-memory field/album-art commands.
/// </summary>
public sealed class RenameCommand : IEditCommand
{
    private readonly IAudioFileService _service;
    private readonly AudioFile _file;
    private readonly string _before;
    private readonly string _after;

    public RenameCommand(IAudioFileService service, AudioFile file, string before, string after)
    {
        _service = service;
        _file = file;
        _before = before;
        _after = after;
        Description = $"Rename {before} → {after}";
    }

    public string Description { get; }

    /// <summary>Forward rename. Throws (and leaves <see cref="AudioFile.FileName"/>
    /// untouched) if the "after" name collides with an existing file, is locked, etc. — see
    /// <see cref="IAudioFileService.Rename"/>.</summary>
    public void Do() => _service.Rename(_file, _after);

    /// <summary>Backward rename. Can independently fail — e.g. if a different file was since
    /// renamed/created to occupy the "before" name — in which case this command is left
    /// exactly where it was on the undo stack (see <see cref="EditHistory.TryUndo"/>'s
    /// TryStep contract).</summary>
    public void Undo() => _service.Rename(_file, _before);
}
