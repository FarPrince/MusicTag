using ATL;
using MusicTag.Core.Models;

namespace MusicTag.Core.Services;

public sealed class AudioFileService : IAudioFileService
{
    public AudioFile Load(string fullPath)
    {
        var (track, ownedStream) = OpenTrackForRead(fullPath);
        try
        {
            return BuildAudioFile(fullPath, track);
        }
        finally
        {
            ownedStream?.Dispose();
        }
    }

    public byte[]? LoadEmbeddedAlbumArt(string fullPath)
    {
        var (track, ownedStream) = OpenTrackForRead(fullPath);
        try
        {
            return track.EmbeddedPictures is { Count: > 0 } pictures
                ? pictures[0].PictureData
                : null;
        }
        finally
        {
            ownedStream?.Dispose();
        }
    }

    public Task SaveAsync(AudioFile file, CancellationToken ct = default)
    {
        // Snapshot what's actually being saved on the calling thread, before the background
        // work starts — see CommitSavedTagEdits' doc comment for why re-reading the live
        // PendingFields/PendingAlbumArt after the write completes would race with a newer
        // in-memory edit made while this save is still in flight.
        var fieldsSnapshot = file.PendingFields;
        var albumArtSnapshot = file.PendingAlbumArt;

        // Captured here (the calling thread — the UI thread in the real app) so the
        // dirty-flag commit can be marshaled back onto it after the disk write finishes on
        // the background thread below. AudioFile derives from ObservableObject and its
        // PropertyChanged is consumed by live WPF bindings (e.g. MainWindow.xaml's
        // DataGridRow DataTrigger on IsDirty), which are thread-affine — raising it from a
        // ThreadPool thread throws. SynchronizationContext is a plain BCL type (not WPF),
        // keeping Core WPF-free; it's null in a headless test host, handled below.
        var callingContext = SynchronizationContext.Current;

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            SaveInternal(file, fieldsSnapshot, albumArtSnapshot, callingContext);
        }, ct);
    }

    public async Task<BatchSaveResult> SaveManyAsync(
        IEnumerable<AudioFile> files,
        IProgress<BatchSaveProgress>? progress = null,
        CancellationToken ct = default)
    {
        var dirtyFiles = files.Where(f => f.IsDirty).ToList();
        var succeeded = new List<AudioFile>();
        var failed = new List<(AudioFile File, Exception Error)>();

        for (var i = 0; i < dirtyFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var file = dirtyFiles[i];
            progress?.Report(new BatchSaveProgress(i, dirtyFiles.Count, file));

            try
            {
                // One file failing (locked, permission denied, unsupported write) must not
                // abort the rest of the batch — isolate per-file via try/catch and collect
                // failures for an end-of-save error report instead.
                await SaveAsync(file, ct).ConfigureAwait(false);
                succeeded.Add(file);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed.Add((file, ex));
            }
        }

        progress?.Report(new BatchSaveProgress(dirtyFiles.Count, dirtyFiles.Count, null));

        return new BatchSaveResult(succeeded, failed);
    }

    public void Rename(AudioFile file, string newFileName)
    {
        if (string.IsNullOrWhiteSpace(newFileName))
        {
            throw new ArgumentException("Filename cannot be empty.", nameof(newFileName));
        }

        // Path.GetInvalidFileNameChars() includes both directory separator characters, so
        // this doubles as a guard against a typed-in "..\..\x.mp3"-style escape out of the
        // current folder, not just literally-illegal characters like ':' or '*'.
        if (newFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"\"{newFileName}\" is not a valid filename.", nameof(newFileName));
        }

        var currentPath = file.FullPath;
        var newPath = Path.Combine(file.DirectoryPath, newFileName);

        // Windows filesystems are case-insensitive but case-preserving, so a rename that only
        // changes case (e.g. "Song.mp3" -> "song.mp3") must not be flagged as "the target
        // already exists" — File.Exists(newPath) would report true because it's the same file.
        var sameFileOnDisk = string.Equals(currentPath, newPath, StringComparison.OrdinalIgnoreCase);

        if (!sameFileOnDisk && File.Exists(newPath))
        {
            throw new RenameTargetExistsException(
                $"A file named \"{newFileName}\" already exists in this folder.");
        }

        // Exact no-op (same name, same case) has nothing to move on disk; a case-only change
        // still needs File.Move (which Windows handles fine even though sameFileOnDisk is true).
        if (!string.Equals(currentPath, newPath, StringComparison.Ordinal))
        {
            File.Move(currentPath, newPath);
        }

        file.CommitRename(newFileName);
    }

    private static void SaveInternal(
        AudioFile file, TagFieldSet fieldsSnapshot, AlbumArtEdit albumArtSnapshot, SynchronizationContext? callingContext)
    {
        var fullPath = file.FullPath;
        var extension = Path.GetExtension(fullPath);
        FileStream? ownedStream = null;

        try
        {
            Track track;
            if (ExtensionParserResolver.TryGetCanonicalExtension(extension, out var canonicalExtension))
            {
                // Same alias mechanism as Load/LoadEmbeddedAlbumArt, but opened ReadWrite
                // this time: Track.Save() writes back into whatever stream (or path) the
                // Track was originally constructed from. This is the write-back half of
                // plan risk #1 — closed out by AudioFileServiceTests' round-trip tests.
                ownedStream = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                track = new Track(ownedStream, canonicalExtension);
            }
            else
            {
                track = new Track(fullPath);
            }

            ApplyPendingFields(track, fieldsSnapshot);
            ApplyPendingAlbumArt(track, albumArtSnapshot);

            // ATL reports save failure via a bool return rather than an exception.
            if (!track.Save())
            {
                throw new TagSaveException($"ATL reported failure while saving tags for '{fullPath}'.");
            }
        }
        finally
        {
            ownedStream?.Dispose();
        }

        // Only reached if track.Save() returned true (an exception above skips this,
        // leaving the file's PendingFields/PendingAlbumArt — and therefore IsDirty — as
        // they were, so a failed save doesn't silently look like it succeeded).
        //
        // Marshaled via Send (not Post) so this still runs on the calling thread when one was
        // captured, and so SaveAsync's returned Task only completes once the dirty-flag
        // commit has actually happened — a caller awaiting SaveAsync and then immediately
        // checking IsDirty must see the post-save state, not a stale in-flight one.
        if (callingContext is not null)
        {
            callingContext.Send(_ => file.CommitSavedTagEdits(fieldsSnapshot, albumArtSnapshot), null);
        }
        else
        {
            file.CommitSavedTagEdits(fieldsSnapshot, albumArtSnapshot);
        }
    }

    private static void ApplyPendingFields(Track track, TagFieldSet fields)
    {
        // ATL treats a null string tag value as "leave whatever is already on disk alone"
        // rather than "clear this field" — so a user who clears a text field (e.g. Comment)
        // down to nothing, which our model represents as null (see EditPanelViewModel/
        // FileListItemViewModel's Normalize()), would otherwise silently fail to actually
        // erase it on save: track.Comment = null just leaves the old value in place. Since
        // ApplyPendingFields always writes the FULL field set on every save (never a partial
        // diff), coercing null to "" here is safe and is the only way to make "clear a field"
        // actually clear the on-disk tag.
        track.Title = fields.Title ?? string.Empty;
        track.Album = fields.Album ?? string.Empty;
        track.Artist = fields.Artist ?? string.Empty;
        track.AlbumArtist = fields.AlbumArtist ?? string.Empty;
        track.Comment = fields.Comment ?? string.Empty;
        track.Composer = fields.Composer ?? string.Empty;
        track.Genre = fields.Genre ?? string.Empty;
        track.Year = fields.Year;
        track.TrackNumber = fields.TrackNumber;
        track.DiscNumber = fields.DiscNumber;
    }

    private static void ApplyPendingAlbumArt(Track track, AlbumArtEdit art)
    {
        // Always Unchanged in M2 (no UI mutates PendingAlbumArt until M6's
        // AlbumArtViewModel), but implemented fully now per plan section 3 so M6 doesn't
        // need to revisit SaveAsync/SaveManyAsync at all.
        switch (art.Action)
        {
            case AlbumArtAction.Unchanged:
                return;

            case AlbumArtAction.Removed:
                track.EmbeddedPictures.Clear();
                return;

            case AlbumArtAction.Replaced:
                track.EmbeddedPictures.Clear();
                if (art.NewImageBytes is { Length: > 0 } bytes)
                {
                    track.EmbeddedPictures.Add(PictureInfo.fromBinaryData(bytes, PictureInfo.PIC_TYPE.Front));
                }

                return;
        }
    }

    private static (Track Track, FileStream? OwnedStream) OpenTrackForRead(string fullPath)
    {
        var extension = Path.GetExtension(fullPath);

        if (ExtensionParserResolver.TryGetCanonicalExtension(extension, out var canonicalExtension))
        {
            // Alias extension (.mkv/.mk3d/.apl/.flc): ATL doesn't register these by
            // extension, so open the file as a stream and construct the Track with the
            // canonical extension as the "mimeType" argument — this routes through ATL's
            // extension-lookup path independent of the real file's name (confirmed at the
            // ATL source level; see ExtensionParserResolver).
            var stream = File.OpenRead(fullPath);
            return (new Track(stream, canonicalExtension), stream);
        }

        return (new Track(fullPath), null);
    }

    private static AudioFile BuildAudioFile(string fullPath, Track track)
    {
        var fields = new TagFieldSet
        {
            Title = track.Title,
            Album = track.Album,
            Artist = track.Artist,
            AlbumArtist = track.AlbumArtist,
            Comment = track.Comment,
            Composer = track.Composer,
            Genre = track.Genre,
            Year = track.Year,
            TrackNumber = track.TrackNumber,
            DiscNumber = track.DiscNumber,
        };

        var fileInfo = new FileInfo(fullPath);
        var channels = track.ChannelsArrangement?.NbChannels ?? 0;
        var codec = track.AudioFormat?.Name ?? "Unknown";
        var tagFormats = track.MetadataFormats is { Count: > 0 }
            ? string.Join(", ", track.MetadataFormats.Select(f => f.Name))
            : string.Empty;

        var extendedInfo = new ExtendedAudioInfo(
            Codec: codec,
            BitrateKbps: track.Bitrate,
            SampleRateHz: (int)track.SampleRate,
            Channels: channels,
            Duration: TimeSpan.FromMilliseconds(track.DurationMs),
            FileSizeBytes: fileInfo.Length,
            IsVbr: track.IsVBR,
            TagFormatsPresent: tagFormats,
            FileModifiedUtc: fileInfo.LastWriteTimeUtc);

        var directoryPath = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var fileName = Path.GetFileName(fullPath);

        return new AudioFile(directoryPath, fileName, fields, extendedInfo);

        // NOTE (deliberate, documented deviation carried over from M1): track's *current*
        // embedded pictures are intentionally not captured into the AudioFile model here —
        // the approved AudioFile shape has PendingAlbumArt (an *edit*, defaulting to
        // Unchanged) but no slot for "current art on disk". M2 covers read-only art display
        // via the separate on-demand IAudioFileService.LoadEmbeddedAlbumArt(fullPath) call
        // instead (see EditPanelViewModel.SetSelection), matching the option M1 flagged as
        // acceptable for M6 to reconsider once art becomes editable.
    }
}
