using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicTag.Core.Models;

/// <summary>
/// Derives from <see cref="ObservableObject"/> (from CommunityToolkit.Mvvm, which has no
/// WPF dependency) rather than a plain POCO so the grid/edit-panel view-models can react
/// to dirty-state changes without Core taking on a WPF reference.
/// </summary>
public sealed class AudioFile : ObservableObject
{
    private TagFieldSet _pendingFields;
    private AlbumArtEdit _pendingAlbumArt = AlbumArtEdit.Unchanged;

    public AudioFile(string directoryPath, string fileName, TagFieldSet originalFields, ExtendedAudioInfo extendedInfo)
    {
        DirectoryPath = directoryPath;
        FileName = fileName;
        OriginalFields = originalFields;
        _pendingFields = originalFields;
        ExtendedInfo = extendedInfo;
    }

    public string DirectoryPath { get; }

    /// <summary>Committed immediately on rename (no "Pending" variant) — filename edits
    /// hit disk right away rather than staging until Save, per the app's design.</summary>
    public string FileName { get; private set; }

    public TagFieldSet OriginalFields { get; private set; }

    /// <summary>
    /// M2 addition (implementation detail only, not a shape change): backed by a field
    /// instead of a plain auto-property so mutating it — e.g. from EditPanelViewModel on
    /// field commit — raises PropertyChanged for both PendingFields and IsDirty. The
    /// grid's dirty-row indicator is a DataTrigger bound to AudioFile.IsDirty (plan
    /// section 5); a plain auto-property would never fire that notification and the
    /// indicator would silently never update.
    /// </summary>
    public TagFieldSet PendingFields
    {
        get => _pendingFields;
        set
        {
            if (SetProperty(ref _pendingFields, value))
            {
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    /// <summary>Same field-backed treatment as <see cref="PendingFields"/> and for the same
    /// reason (IsDirty change notification). Still always Unchanged in M2 — no UI mutates
    /// it until M6 — but SaveAsync/SaveManyAsync already honor it (see AudioFileService).</summary>
    public AlbumArtEdit PendingAlbumArt
    {
        get => _pendingAlbumArt;
        set
        {
            if (SetProperty(ref _pendingAlbumArt, value))
            {
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public ExtendedAudioInfo ExtendedInfo { get; private set; }

    public string FullPath => Path.Combine(DirectoryPath, FileName);

    public bool IsDirty => PendingFields != OriginalFields || PendingAlbumArt.Action != AlbumArtAction.Unchanged;

    public void CommitPendingTagEdits()
    {
        OriginalFields = PendingFields;
        PendingAlbumArt = AlbumArtEdit.Unchanged;
        OnPropertyChanged(nameof(IsDirty));
    }

    public void CommitRename(string newFileName)
    {
        FileName = newFileName;
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(FullPath));
    }
}
