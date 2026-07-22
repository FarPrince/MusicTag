using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicTag.Core.History;
using MusicTag.Core.Models;

namespace MusicTag.App.ViewModels;

/// <summary>
/// M5 scope: rebuilds its bound state whenever <see cref="MainWindowViewModel"/>'s grid
/// selection changes (<see cref="SetSelection"/>), for any selection size — 0/1/N are all one
/// code path now rather than special-cased, per plan section 5:
/// - 0 selected -> fields disabled/blank.
/// - 1 selected -> fields effectively bound directly to that file's PendingFields.X (the M2/M3
///   behavior — a selection of exactly one is just the "unanimous" case of a 1-element set).
/// - N selected -> each field also gets a <see cref="MixedValue{T}"/> snapshot (IsMixed +
///   Value) computed by distinctness across the selection; EditPanelView.xaml's converter
///   (<see cref="Converters.MixedValuePlaceholderConverter"/>) turns that into a "&lt;keep&gt;"
///   PlaceholderText hint when mixed. The actual bound Text value (e.g. <see cref="Title"/>)
///   is null/blank whenever the field is mixed, real Text otherwise — see the doc comment on
///   <see cref="MixedValue{T}"/> for why the editable value stays a plain property instead of
///   the MixedValue struct itself.
///
/// On commit (Enter/focus-lost), <see cref="CommitField"/> builds one leaf
/// <see cref="FieldEditCommand"/> per selected file (skipping any file where the new value
/// wouldn't actually change anything) and wraps them in a single
/// <see cref="CompositeEditCommand"/> pushed via one <see cref="EditHistory.Execute"/> call —
/// one undo step for the whole batch, exactly per plan section 4: "A single-file edit is just
/// the N==1 case." Only the field the user actually edited is ever included — untouched
/// fields (mixed or not) never generate a leaf command, because their bound property's setter
/// (CommunityToolkit's generated equality check) simply never fires unless the displayed value
/// actually changes, the same guard <see cref="_isLoadingSelection"/> already relies on to stop
/// <see cref="RefreshFieldsFromSelection"/>'s own repopulation from looking like a user edit.
///
/// Filename is never shown in this panel (renaming is exclusively an inline grid action, see
/// M4's RenameFileInline) — <see cref="IsMultiSelection"/> instead drives a short hint in
/// EditPanelView.xaml pointing at the grid's F2/double-click rename whenever more than one
/// file is selected, per plan section 5's "Filename is excluded from this panel entirely when
/// &gt;1 file is selected."
/// </summary>
public sealed partial class EditPanelViewModel : ObservableObject
{
    private readonly EditHistory _editHistory;
    private IReadOnlyList<AudioFile> _selectedFiles = Array.Empty<AudioFile>();
    private bool _isLoadingSelection;

    public EditPanelViewModel(EditHistory editHistory)
    {
        _editHistory = editHistory;
    }

    [ObservableProperty]
    private bool isEnabled;

    /// <summary>Drives EditPanelView.xaml's inline rename hint — true whenever more than one
    /// file is selected. Filename itself is never editable from this panel regardless of
    /// selection size (see class doc comment).</summary>
    [ObservableProperty]
    private bool isMultiSelection;

    [ObservableProperty]
    private int selectedFileCount;

    /// <summary>Drives EditPanelView.xaml's read-only "Technical Info" section — the
    /// original request's "read-only view of deeper technical metadata" (plan's
    /// <see cref="Core.Models.ExtendedAudioInfo"/> model). Shown only for exactly one selected
    /// file: the underlying data (bitrate/sample rate/codec/etc.) is per-file reference
    /// information with no sensible "mixed value" story the way tag fields have, so rather
    /// than inventing one this simply hides the section for 0 or N>1 selections, the same way
    /// the multi-selection Filename hint above hides/shows based on selection size.</summary>
    [ObservableProperty]
    private bool isExtendedInfoVisible;

    [ObservableProperty]
    private string? extendedInfoSummary;

    [ObservableProperty]
    private string? title;

    [ObservableProperty]
    private MixedValue<string?> titleMixed;

    [ObservableProperty]
    private string? album;

    [ObservableProperty]
    private MixedValue<string?> albumMixed;

    [ObservableProperty]
    private string? artist;

    [ObservableProperty]
    private MixedValue<string?> artistMixed;

    [ObservableProperty]
    private string? albumArtist;

    [ObservableProperty]
    private MixedValue<string?> albumArtistMixed;

    [ObservableProperty]
    private string? comment;

    [ObservableProperty]
    private MixedValue<string?> commentMixed;

    [ObservableProperty]
    private string? composer;

    [ObservableProperty]
    private MixedValue<string?> composerMixed;

    [ObservableProperty]
    private string? genre;

    [ObservableProperty]
    private MixedValue<string?> genreMixed;

    [ObservableProperty]
    private int? year;

    [ObservableProperty]
    private MixedValue<int?> yearMixed;

    [ObservableProperty]
    private int? trackNumber;

    [ObservableProperty]
    private MixedValue<int?> trackNumberMixed;

    [ObservableProperty]
    private int? discNumber;

    [ObservableProperty]
    private MixedValue<int?> discNumberMixed;

    /// <summary>Called by <c>MainWindowViewModel</c> whenever the grid's bound
    /// <c>SelectedFiles</c> collection changes (added via the M5
    /// <see cref="Behaviors.DataGridSelectedItemsBehavior"/>). Pass an empty list for "nothing
    /// selected" — there is no longer a separate 0/1/N special case at this call site.</summary>
    public void SetSelection(IReadOnlyList<AudioFile> files)
    {
        foreach (var file in _selectedFiles)
            file.PropertyChanged -= OnSelectedFilePropertyChanged;

        _isLoadingSelection = true;
        try
        {
            _selectedFiles = files;
            foreach (var file in _selectedFiles)
                file.PropertyChanged += OnSelectedFilePropertyChanged;

            IsEnabled = _selectedFiles.Count > 0;
            IsMultiSelection = _selectedFiles.Count > 1;
            SelectedFileCount = _selectedFiles.Count;

            RefreshFieldsFromSelection();
        }
        finally
        {
            _isLoadingSelection = false;
        }
    }

    partial void OnTitleChanged(string? value) => CommitField("Title", f => f with { Title = Normalize(value) });

    partial void OnAlbumChanged(string? value) => CommitField("Album", f => f with { Album = Normalize(value) });

    partial void OnArtistChanged(string? value) => CommitField("Artist", f => f with { Artist = Normalize(value) });

    partial void OnAlbumArtistChanged(string? value) => CommitField("Album Artist", f => f with { AlbumArtist = Normalize(value) });

    partial void OnCommentChanged(string? value) => CommitField("Comment", f => f with { Comment = Normalize(value) });

    partial void OnComposerChanged(string? value) => CommitField("Composer", f => f with { Composer = Normalize(value) });

    partial void OnGenreChanged(string? value) => CommitField("Genre", f => f with { Genre = Normalize(value) });

    partial void OnYearChanged(int? value) => CommitField("Year", f => f with { Year = value });

    partial void OnTrackNumberChanged(int? value) => CommitField("Track #", f => f with { TrackNumber = value });

    partial void OnDiscNumberChanged(int? value) => CommitField("Disc #", f => f with { DiscNumber = value });

    /// <summary>Reacts to any currently selected file's PendingFields changing out from under
    /// this view model — i.e. an Undo/Redo (or another view's edit of the same file) applied
    /// via EditHistory rather than this class's own <see cref="CommitField"/> — so the visible
    /// text boxes/mixed-value hints stay in sync instead of silently going stale. Guarded by
    /// <see cref="_isLoadingSelection"/> the same way <see cref="SetSelection"/> is, so
    /// re-populating fields here never turns around and commits a redundant no-op edit back
    /// into EditHistory.</summary>
    private void OnSelectedFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AudioFile.PendingFields))
            return;

        var wasLoading = _isLoadingSelection;
        _isLoadingSelection = true;
        try
        {
            RefreshFieldsFromSelection();
        }
        finally
        {
            _isLoadingSelection = wasLoading;
        }
    }

    private void RefreshFieldsFromSelection()
    {
        var files = _selectedFiles;

        IsExtendedInfoVisible = files.Count == 1;
        ExtendedInfoSummary = files.Count == 1 ? FormatExtendedInfo(files[0]) : null;

        TitleMixed = MixedValue<string?>.From(files.Select(f => f.PendingFields.Title));
        Title = TitleMixed.Value;

        AlbumMixed = MixedValue<string?>.From(files.Select(f => f.PendingFields.Album));
        Album = AlbumMixed.Value;

        ArtistMixed = MixedValue<string?>.From(files.Select(f => f.PendingFields.Artist));
        Artist = ArtistMixed.Value;

        AlbumArtistMixed = MixedValue<string?>.From(files.Select(f => f.PendingFields.AlbumArtist));
        AlbumArtist = AlbumArtistMixed.Value;

        CommentMixed = MixedValue<string?>.From(files.Select(f => f.PendingFields.Comment));
        Comment = CommentMixed.Value;

        ComposerMixed = MixedValue<string?>.From(files.Select(f => f.PendingFields.Composer));
        Composer = ComposerMixed.Value;

        GenreMixed = MixedValue<string?>.From(files.Select(f => f.PendingFields.Genre));
        Genre = GenreMixed.Value;

        YearMixed = MixedValue<int?>.From(files.Select(f => f.PendingFields.Year));
        Year = YearMixed.Value;

        TrackNumberMixed = MixedValue<int?>.From(files.Select(f => f.PendingFields.TrackNumber));
        TrackNumber = TrackNumberMixed.Value;

        DiscNumberMixed = MixedValue<int?>.From(files.Select(f => f.PendingFields.DiscNumber));
        DiscNumber = DiscNumberMixed.Value;
    }

    /// <summary>Builds one <see cref="FieldEditCommand"/> per selected file that
    /// <paramref name="mutate"/> would actually change (files already matching the new value
    /// are simply skipped — harmless either way since assigning an equal record is a no-op,
    /// but skipping keeps the composite's child list meaningful), wraps them in a single
    /// <see cref="CompositeEditCommand"/>, and pushes that via one
    /// <see cref="EditHistory.Execute"/> call — one undo step for the whole batch. A selection
    /// of exactly one file degenerates to the old M2/M3 single-leaf shape.</summary>
    private void CommitField(string fieldLabel, Func<TagFieldSet, TagFieldSet> mutate)
    {
        // Guards against SetSelection's/OnSelectedFilePropertyChanged's own property
        // assignments (switching selection, or reflecting an external undo/redo) being
        // mistaken for a user edit — without this, re-populating Title/Album/etc. would
        // immediately push a spurious command back into EditHistory.
        if (_isLoadingSelection || _selectedFiles.Count == 0)
            return;

        var leaves = new List<IEditCommand>();
        foreach (var file in _selectedFiles)
        {
            var before = file.PendingFields;
            var after = mutate(before);
            if (after == before)
                continue; // This file already matches the newly typed value — nothing to record.

            leaves.Add(new FieldEditCommand(file, before, after, $"Set {fieldLabel} on {file.FileName}"));
        }

        if (leaves.Count == 0)
            return; // Nothing actually changed on any selected file.

        var description = leaves.Count == 1
            ? leaves[0].Description
            : $"Set {fieldLabel} on {leaves.Count} files";

        _editHistory.Execute(new CompositeEditCommand(description, leaves));
    }

    private static string? Normalize(string? value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>Formats <see cref="AudioFile.ExtendedInfo"/> into the single-line, read-only
    /// technical-metadata summary shown by EditPanelView.xaml's Technical Info section — the
    /// bitrate/sample-rate/codec/etc. reference information the plan's ExtendedAudioInfo model
    /// carries but that, before this fix, no view ever displayed.</summary>
    private static string FormatExtendedInfo(AudioFile file)
    {
        var info = file.ExtendedInfo;
        var channelsLabel = info.Channels switch
        {
            1 => "Mono",
            2 => "Stereo",
            0 => "Unknown",
            _ => $"{info.Channels}ch",
        };
        var vbrLabel = info.IsVbr ? "VBR" : "CBR";
        var fileSizeLabel = FormatFileSize(info.FileSizeBytes);
        var tagFormats = string.IsNullOrEmpty(info.TagFormatsPresent) ? "None" : info.TagFormatsPresent;

        return $"Codec: {info.Codec}\n" +
               $"Bitrate: {info.BitrateKbps} kbps ({vbrLabel})\n" +
               $"Sample Rate: {info.SampleRateHz:N0} Hz\n" +
               $"Channels: {channelsLabel}\n" +
               $"File Size: {fileSizeLabel}\n" +
               $"Tag Formats: {tagFormats}\n" +
               $"Modified: {info.FileModifiedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
    }

    private static string FormatFileSize(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        return bytes >= mb ? $"{bytes / mb:0.0} MB" : $"{bytes / kb:0.0} KB";
    }
}
