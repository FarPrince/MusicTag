using MusicTag.Core.History;
using MusicTag.Core.Models;
using MusicTag.Core.Services;

namespace MusicTag.Tests;

/// <summary>
/// Undo/redo semantics for EditHistory, including CompositeEditCommand batch ordering and the
/// TryExecute/TryUndo/TryRedo failure paths, per plan section 9. The generic (non-rename)
/// failure-path tests use a small fake IEditCommand that throws on demand, proving EditHistory
/// leaves its stacks untouched on a failed Do()/Undo()/Redo() independent of which command type
/// is involved. The RenameCommand-specific tests further down (M4) exercise the one real
/// command type this matters for in practice, against a fake IAudioFileService — no real disk
/// I/O — per plan section 9's "simulate a RenameCommand whose Do() throws" instruction.
/// </summary>
public class EditHistoryTests
{
    private static AudioFile MakeFile(string title = "Original")
        => new("C:\\music", "song.mp3",
            new TagFieldSet { Title = title },
            new ExtendedAudioInfo("MP3", 320, 44100, 2, TimeSpan.FromSeconds(180), 0, false, "ID3v2", DateTime.UtcNow));

    [Fact]
    public void Execute_AppliesCommand_AndPushesOntoUndoStack()
    {
        var history = new EditHistory();
        var file = MakeFile();
        var before = file.PendingFields;
        var after = before with { Title = "Changed" };

        history.Execute(new FieldEditCommand(file, before, after, "Set Title"));

        Assert.Equal("Changed", file.PendingFields.Title);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal("Set Title", history.TopUndoDescription);
    }

    [Fact]
    public void Execute_ClearsRedoStack()
    {
        var history = new EditHistory();
        var file = MakeFile();
        var v1 = file.PendingFields;
        var v2 = v1 with { Title = "V2" };
        var v3 = v2 with { Title = "V3" };

        history.Execute(new FieldEditCommand(file, v1, v2, "To V2"));
        history.TryUndo(out _);
        Assert.True(history.CanRedo);

        history.Execute(new FieldEditCommand(file, v1, v3, "To V3"));

        Assert.False(history.CanRedo);
    }

    [Fact]
    public void TryUndo_TryRedo_RoundTripsFieldValue()
    {
        var history = new EditHistory();
        var file = MakeFile();
        var before = file.PendingFields;
        var after = before with { Title = "Changed" };

        history.Execute(new FieldEditCommand(file, before, after, "Set Title"));

        Assert.True(history.TryUndo(out var undoError));
        Assert.Null(undoError);
        Assert.Equal("Original", file.PendingFields.Title);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);

        Assert.True(history.TryRedo(out var redoError));
        Assert.Null(redoError);
        Assert.Equal("Changed", file.PendingFields.Title);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void TryUndo_OnEmptyStack_IsANoOpSuccess()
    {
        var history = new EditHistory();

        Assert.True(history.TryUndo(out var error));
        Assert.Null(error);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void CompositeEditCommand_AppliesLeavesInOrder_AndUndoesInReverse()
    {
        var history = new EditHistory();
        var fileA = MakeFile("A-Original");
        var fileB = MakeFile("B-Original");
        var order = new List<string>();

        var cmdA = new RecordingCommand("A", () => order.Add("A.Do"), () => order.Add("A.Undo"));
        var cmdB = new RecordingCommand("B", () => order.Add("B.Do"), () => order.Add("B.Undo"));

        var composite = new CompositeEditCommand("Batch edit", new IEditCommand[] { cmdA, cmdB });

        history.Execute(composite);
        Assert.Equal(new[] { "A.Do", "B.Do" }, order);

        order.Clear();
        Assert.True(history.TryUndo(out _));
        Assert.Equal(new[] { "B.Undo", "A.Undo" }, order);
    }

    [Fact]
    public void CompositeEditCommand_WithNoChildren_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CompositeEditCommand("Empty", Array.Empty<IEditCommand>()));
    }

    /// <summary>M5 scope: EditPanelViewModel's batch commit builds exactly this shape — one
    /// FieldEditCommand per affected file, wrapped in a single CompositeEditCommand, pushed via
    /// one EditHistory.Execute call. This test exercises that shape directly against real
    /// AudioFile/FieldEditCommand instances (rather than the abstract RecordingCommand used
    /// above) so the "one undo step for the whole batch" guarantee is proven for the actual
    /// leaf command type multi-select editing uses, including each file keeping its own
    /// independent before-value on Undo (a genuinely mixed starting selection).</summary>
    [Fact]
    public void CompositeEditCommand_BatchFieldEditAcrossMultipleFiles_IsOneUndoStep_AndRestoresEachFilesOwnValue()
    {
        var history = new EditHistory();
        var fileA = MakeFile();
        var fileB = MakeFile();

        // Mixed starting Genre values across the two files — each file's actual current
        // PendingFields is set to match "before" so Undo genuinely restores reality, not just
        // whatever value happens to be passed to FieldEditCommand's constructor.
        var beforeA = fileA.PendingFields with { Genre = "Rock" };
        var beforeB = fileB.PendingFields with { Genre = "Jazz" };
        fileA.PendingFields = beforeA;
        fileB.PendingFields = beforeB;

        var afterA = beforeA with { Genre = "Metal" };
        var afterB = beforeB with { Genre = "Metal" }; // User types one literal value for all selected files.

        var composite = new CompositeEditCommand(
            "Set Genre on 2 files",
            new IEditCommand[]
            {
                new FieldEditCommand(fileA, beforeA, afterA, "Set Genre on a.mp3"),
                new FieldEditCommand(fileB, beforeB, afterB, "Set Genre on b.mp3"),
            });

        history.Execute(composite);

        Assert.Equal("Metal", fileA.PendingFields.Genre);
        Assert.Equal("Metal", fileB.PendingFields.Genre);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal("Set Genre on 2 files", history.TopUndoDescription);

        // One Ctrl+Z undoes the whole batch in a single step, restoring each file's own
        // original (mixed) value rather than some shared/averaged one.
        Assert.True(history.TryUndo(out var undoError));
        Assert.Null(undoError);
        Assert.Equal("Rock", fileA.PendingFields.Genre);
        Assert.Equal("Jazz", fileB.PendingFields.Genre);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);

        Assert.True(history.TryRedo(out var redoError));
        Assert.Null(redoError);
        Assert.Equal("Metal", fileA.PendingFields.Genre);
        Assert.Equal("Metal", fileB.PendingFields.Genre);
    }

    /// <summary>M6 scope: AlbumArtViewModel's Replace/Remove build exactly this shape — one
    /// AlbumArtEditCommand per currently selected file, wrapped in a single
    /// CompositeEditCommand, pushed via one EditHistory.Execute call — mirroring the M5 test
    /// above for FieldEditCommand, but for the all-or-nothing album-art batch-apply path
    /// instead (plan section 5: "Replace/Remove apply to the entire current selection as one
    /// all-or-nothing CompositeEditCommand of AlbumArtEditCommands").</summary>
    [Fact]
    public void CompositeEditCommand_BatchAlbumArtEditAcrossMultipleFiles_IsOneUndoStep_AndRestoresEachFilesOwnValue()
    {
        var history = new EditHistory();
        var fileA = MakeFile();
        var fileB = MakeFile();

        // Mixed starting album-art state: A already has some art, B has none — proving Undo
        // restores each file's own prior state rather than a shared one, same as the
        // FieldEditCommand batch test above.
        var beforeA = new AlbumArtEdit(AlbumArtAction.Replaced, new byte[] { 1, 2, 3 });
        var beforeB = AlbumArtEdit.Unchanged;
        fileA.PendingAlbumArt = beforeA;
        fileB.PendingAlbumArt = beforeB;

        var after = new AlbumArtEdit(AlbumArtAction.Replaced, new byte[] { 9, 9, 9 });

        var composite = new CompositeEditCommand(
            "Replace album art on 2 files",
            new IEditCommand[]
            {
                new AlbumArtEditCommand(fileA, beforeA, after, "Replace album art on a.mp3"),
                new AlbumArtEditCommand(fileB, beforeB, after, "Replace album art on b.mp3"),
            });

        history.Execute(composite);

        Assert.Equal(after, fileA.PendingAlbumArt);
        Assert.Equal(after, fileB.PendingAlbumArt);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);

        Assert.True(history.TryUndo(out var undoError));
        Assert.Null(undoError);
        Assert.Equal(beforeA, fileA.PendingAlbumArt);
        Assert.Equal(beforeB, fileB.PendingAlbumArt);

        Assert.True(history.TryRedo(out var redoError));
        Assert.Null(redoError);
        Assert.Equal(after, fileA.PendingAlbumArt);
        Assert.Equal(after, fileB.PendingAlbumArt);
    }

    [Fact]
    public void TryExecute_WhenDoThrows_LeavesStacksUntouched()
    {
        var history = new EditHistory();
        var failing = new RecordingCommand("Fails", () => throw new InvalidOperationException("boom"), () => { });

        var result = history.TryExecute(failing, out var error);

        Assert.False(result);
        Assert.IsType<InvalidOperationException>(error);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void TryUndo_WhenUndoThrows_LeavesCommandOnUndoStack_AndRedoStackUntouched()
    {
        var history = new EditHistory();
        var failing = new RecordingCommand("Fails", () => { }, () => throw new InvalidOperationException("collision"));

        history.Execute(failing);
        Assert.True(history.CanUndo);

        var result = history.TryUndo(out var error);

        Assert.False(result);
        Assert.IsType<InvalidOperationException>(error);
        // Left exactly as-is on failure — the command must still be the top of the undo
        // stack (not moved to redo, not dropped).
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal("Fails", history.TopUndoDescription);
    }

    [Fact]
    public void TryRedo_WhenDoThrows_LeavesCommandOnRedoStack_AndUndoStackUntouched()
    {
        var history = new EditHistory();
        var callCount = 0;
        var failing = new RecordingCommand(
            "Fails",
            doAction: () =>
            {
                callCount++;
                if (callCount > 1)
                    throw new InvalidOperationException("collision on redo");
            },
            undoAction: () => { });

        history.Execute(failing); // callCount == 1, succeeds
        history.TryUndo(out _);
        Assert.True(history.CanRedo);

        var result = history.TryRedo(out var error);

        Assert.False(result);
        Assert.IsType<InvalidOperationException>(error);
        Assert.True(history.CanRedo);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Clear_EmptiesBothStacks()
    {
        var history = new EditHistory();
        var file = MakeFile();
        var before = file.PendingFields;
        var after = before with { Title = "Changed" };

        history.Execute(new FieldEditCommand(file, before, after, "Set Title"));
        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Changed_FiresOnExecuteAndTryUndoAndTryRedo()
    {
        var history = new EditHistory();
        var file = MakeFile();
        var before = file.PendingFields;
        var after = before with { Title = "Changed" };
        var raiseCount = 0;
        history.Changed += (_, _) => raiseCount++;

        history.Execute(new FieldEditCommand(file, before, after, "Set Title"));
        Assert.Equal(1, raiseCount);

        history.TryUndo(out _);
        Assert.Equal(2, raiseCount);

        history.TryRedo(out _);
        Assert.Equal(3, raiseCount);
    }

    [Fact]
    public void RenameCommand_TryExecute_RenamesFile_AndTryUndoRoundTrips()
    {
        var history = new EditHistory();
        var service = new FakeAudioFileService("song.mp3");
        var file = MakeFile();

        var command = new RenameCommand(service, file, "song.mp3", "renamed.mp3");

        Assert.True(history.TryExecute(command, out var executeError));
        Assert.Null(executeError);
        Assert.Equal("renamed.mp3", file.FileName);
        Assert.True(history.CanUndo);

        Assert.True(history.TryUndo(out var undoError));
        Assert.Null(undoError);
        Assert.Equal("song.mp3", file.FileName);
        Assert.True(history.CanRedo);
    }

    [Fact]
    public void RenameCommand_TryExecute_WhenTargetNameExists_LeavesStackUntouched_AndFileNameUnchanged()
    {
        var history = new EditHistory();
        // Folder already contains "taken.mp3" — a different file than the one being renamed.
        var service = new FakeAudioFileService("song.mp3", "taken.mp3");
        var file = MakeFile();
        file.CommitRename("song.mp3");

        var command = new RenameCommand(service, file, "song.mp3", "taken.mp3");

        var result = history.TryExecute(command, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Equal("song.mp3", file.FileName); // Do() threw before CommitRename ran.
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    /// <summary>The exact scenario plan section 4/9 calls out by name: undoing a rename can
    /// independently fail if, since the rename happened, some other file has come to occupy
    /// the original name — TryUndo must surface that as an error (for RenameErrorDialog) and
    /// leave the command exactly on top of the undo stack rather than corrupting it or
    /// silently no-op'ing.</summary>
    [Fact]
    public void RenameCommand_TryUndo_WhenOriginalNameNowCollidesWithAnotherFile_SurfacesError_AndLeavesUndoStackIntact()
    {
        var history = new EditHistory();
        var service = new FakeAudioFileService("track03.mp3");
        var file = MakeFile();
        file.CommitRename("track03.mp3");

        var renameCommand = new RenameCommand(service, file, "track03.mp3", "03 - Song.mp3");
        Assert.True(history.TryExecute(renameCommand, out _));
        Assert.Equal("03 - Song.mp3", file.FileName);

        // Simulate "a different file created since" occupying the original name.
        service.RegisterExistingFile("track03.mp3");

        var result = history.TryUndo(out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.IsType<RenameTargetExistsException>(error);
        // Left exactly as-is on failure: still the top of the undo stack, file name
        // untouched by the failed Undo() call.
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal("03 - Song.mp3", file.FileName);
        Assert.Equal($"Rename track03.mp3 → 03 - Song.mp3", history.TopUndoDescription);
    }

    /// <summary>Minimal fake IEditCommand for exercising failure paths without depending on
    /// RenameCommand or any real disk I/O.</summary>
    private sealed class RecordingCommand : IEditCommand
    {
        private readonly Action _doAction;
        private readonly Action _undoAction;

        public RecordingCommand(string description, Action doAction, Action undoAction)
        {
            Description = description;
            _doAction = doAction;
            _undoAction = undoAction;
        }

        public string Description { get; }

        public void Do() => _doAction();

        public void Undo() => _undoAction();
    }

    /// <summary>Fake IAudioFileService used only to exercise RenameCommand/EditHistory
    /// interaction without any real disk I/O — tracks a simple in-memory set of filenames
    /// "present in the folder" and mirrors AudioFileService.Rename's real collision-check
    /// semantics (case-insensitive, ignoring the file's own current name) closely enough to
    /// prove EditHistory's TryExecute/TryUndo contract against the one command type that can
    /// actually fail. Every other IAudioFileService member is unused by these tests.</summary>
    private sealed class FakeAudioFileService : IAudioFileService
    {
        private readonly HashSet<string> _existingNames;

        public FakeAudioFileService(params string[] existingNames)
            => _existingNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        /// <summary>Simulates another file independently being created/renamed to occupy
        /// <paramref name="fileName"/> after the fact — without going through this service's
        /// own Rename (which would also update AudioFile.FileName, not appropriate here since
        /// no AudioFile actually holds this name in these tests).</summary>
        public void RegisterExistingFile(string fileName) => _existingNames.Add(fileName);

        public void Rename(AudioFile file, string newFileName)
        {
            var collides = _existingNames.Contains(newFileName)
                && !string.Equals(newFileName, file.FileName, StringComparison.OrdinalIgnoreCase);

            if (collides)
            {
                throw new RenameTargetExistsException(
                    $"A file named \"{newFileName}\" already exists in this folder.");
            }

            _existingNames.Remove(file.FileName);
            _existingNames.Add(newFileName);
            file.CommitRename(newFileName);
        }

        public AudioFile Load(string fullPath) => throw new NotSupportedException("Not used by these tests.");

        public byte[]? LoadEmbeddedAlbumArt(string fullPath) => throw new NotSupportedException("Not used by these tests.");

        public Task SaveAsync(AudioFile file, CancellationToken ct = default) => throw new NotSupportedException("Not used by these tests.");

        public Task<BatchSaveResult> SaveManyAsync(
            IEnumerable<AudioFile> files,
            IProgress<BatchSaveProgress>? progress = null,
            CancellationToken ct = default) => throw new NotSupportedException("Not used by these tests.");
    }
}
