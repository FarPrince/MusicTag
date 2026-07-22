using MusicTag.Core.Models;

namespace MusicTag.Core.Services;

public interface IAudioFileService
{
    AudioFile Load(string fullPath);

    /// <summary>Reads the first embedded picture directly from disk (independent of
    /// <see cref="AudioFile"/>, which has no "current art" slot — only
    /// <see cref="AudioFile.PendingAlbumArt"/>, an edit). Added in M2 to support read-only
    /// album art display in the edit panel; M6 (replace/remove/paste) will decide whether
    /// to cache this on AudioFile or keep loading on demand per-selection, as flagged as an
    /// open question in M1. Returns null if the file has no embedded art.</summary>
    byte[]? LoadEmbeddedAlbumArt(string fullPath);

    /// <summary>Persists PendingFields + PendingAlbumArt only — never the filename (see
    /// <see cref="Rename"/>). Commits the file's pending edits (<see cref="AudioFile.CommitPendingTagEdits"/>)
    /// on success. Throws <see cref="TagSaveException"/> if ATL reports failure.</summary>
    Task SaveAsync(AudioFile file, CancellationToken ct = default);

    /// <summary>Saves every dirty file in <paramref name="files"/> (files that are already
    /// clean are skipped entirely), isolating per-file failures so one locked/permission
    /// -denied file doesn't abort the batch.</summary>
    Task<BatchSaveResult> SaveManyAsync(
        IEnumerable<AudioFile> files,
        IProgress<BatchSaveProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>Performs an immediate <see cref="File.Move"/> — disk I/O happens here, not
    /// at Save time, matching the app's live-rename behavior. Validates the target filename
    /// doesn't already belong to a different file first and throws
    /// <see cref="RenameTargetExistsException"/> for that specific, common case rather than a
    /// raw <see cref="IOException"/>; other failures (locked file, permission denied, invalid
    /// characters) surface with their natural .NET exception. On success, calls
    /// <see cref="AudioFile.CommitRename"/>. Callers (<see cref="History.RenameCommand"/> via
    /// <see cref="History.EditHistory.TryExecute"/>/<see cref="History.EditHistory.TryUndo"/>/
    /// <see cref="History.EditHistory.TryRedo"/>) must not treat a throw here as having
    /// mutated anything — it hasn't.</summary>
    void Rename(AudioFile file, string newFileName);
}
