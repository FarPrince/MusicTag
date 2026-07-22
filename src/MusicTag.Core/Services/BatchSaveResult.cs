using MusicTag.Core.Models;

namespace MusicTag.Core.Services;

/// <summary>
/// Result of a <see cref="IAudioFileService.SaveManyAsync"/> batch: every file that saved
/// cleanly, plus every file that failed paired with the exception that caused the failure,
/// so the caller can render an end-of-save error report without one bad file aborting the
/// whole batch. Not yet produced in M1 (SaveManyAsync is a NotImplementedException stub) —
/// the shape exists now so later milestones don't need to change the interface.
/// </summary>
public sealed record BatchSaveResult(
    IReadOnlyList<AudioFile> Succeeded,
    IReadOnlyList<(AudioFile File, Exception Error)> Failed);

/// <summary>Progress notification for a batch save, reported via <see cref="IProgress{T}"/>.</summary>
public sealed record BatchSaveProgress(int CompletedCount, int TotalCount, AudioFile? CurrentFile);
