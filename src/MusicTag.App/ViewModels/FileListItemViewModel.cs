using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicTag.Core.History;
using MusicTag.Core.Models;

namespace MusicTag.App.ViewModels;

/// <summary>
/// Grid-row view model wrapping one <see cref="AudioFile"/>.
///
/// Originally (M2-M8) this only exposed read-only pass-through properties, with all actual
/// field editing happening in the side <see cref="EditPanelViewModel"/> — the grid's own
/// Title/Artist/Album/Track#/Year columns were marked read-only. Per direct user feedback
/// ("unable to double click on fields in the main view and edit"), these are now real,
/// independently-editable properties: committing one (via the grid's own double-click/F2
/// cell editing, exactly like Filename already worked) builds a <see cref="FieldEditCommand"/>
/// for just this one file and pushes it through the same shared <see cref="EditHistory"/> the
/// edit panel uses — so editing a value in the grid and editing it in the side panel are two
/// entry points into the exact same undo-tracked, auto-saved commit path, not two divergent
/// systems. <see cref="_isSyncingFromAudioFile"/> guards against re-entrant commits when a
/// property is being *refreshed* here (because the same file changed via the edit panel, or an
/// undo/redo) rather than actually being *edited* here — mirrors
/// <see cref="EditPanelViewModel"/>'s own <c>_isLoadingSelection</c> guard for the identical
/// reason.
/// </summary>
public sealed partial class FileListItemViewModel : ObservableObject, IDisposable
{
    private readonly EditHistory _editHistory;
    private bool _isSyncingFromAudioFile;

    public FileListItemViewModel(AudioFile audioFile, EditHistory editHistory)
    {
        AudioFile = audioFile;
        _editHistory = editHistory;
        AudioFile.PropertyChanged += OnAudioFilePropertyChanged;
        SyncFieldsFromAudioFile();
    }

    public AudioFile AudioFile { get; }

    public string FileName => AudioFile.FileName;

    [ObservableProperty]
    private string? title;

    [ObservableProperty]
    private string? artist;

    [ObservableProperty]
    private string? album;

    [ObservableProperty]
    private string? albumArtist;

    [ObservableProperty]
    private string? genre;

    [ObservableProperty]
    private string? comment;

    [ObservableProperty]
    private string? composer;

    [ObservableProperty]
    private int? year;

    [ObservableProperty]
    private int? trackNumber;

    [ObservableProperty]
    private int? discNumber;

    public string Duration => AudioFile.ExtendedInfo.Duration.ToString(
        AudioFile.ExtendedInfo.Duration.Hours > 0 ? @"h\:mm\:ss" : @"m\:ss");

    /// <summary>Read-only technical-metadata columns, per user feedback ("let me select
    /// technical info onto the main viewport as headers") — the same <see cref="AudioFile.ExtendedInfo"/>
    /// values EditPanelViewModel's "Technical Info" summary already surfaces for a single
    /// selection, now also individually selectable as grid columns (hidden by default — see
    /// MainWindowViewModel's IsXColumnVisible properties and MainWindow.xaml.cs's column
    /// chooser) so they're visible across every row at once instead of only one file at a
    /// time.</summary>
    public string Codec => AudioFile.ExtendedInfo.Codec;

    public string Bitrate => $"{AudioFile.ExtendedInfo.BitrateKbps} kbps";

    public string SampleRate => $"{AudioFile.ExtendedInfo.SampleRateHz:N0} Hz";

    public string Channels => AudioInfoFormatting.FormatChannels(AudioFile.ExtendedInfo.Channels);

    public string FileSize => AudioInfoFormatting.FormatFileSize(AudioFile.ExtendedInfo.FileSizeBytes);

    public string TagFormats => string.IsNullOrEmpty(AudioFile.ExtendedInfo.TagFormatsPresent)
        ? "None" : AudioFile.ExtendedInfo.TagFormatsPresent;

    public string Modified => AudioFile.ExtendedInfo.FileModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    /// <summary>Passthrough used by MainWindowViewModel to recompute its pending-changes
    /// status-bar count without subscribing to every AudioFile directly. The grid's own
    /// dirty-row DataTrigger still binds the nested `AudioFile.IsDirty` path directly.</summary>
    public bool IsDirty => AudioFile.IsDirty;

    public void Dispose() => AudioFile.PropertyChanged -= OnAudioFilePropertyChanged;

    partial void OnTitleChanged(string? value) => CommitField("Title", f => f with { Title = Normalize(value) });

    partial void OnArtistChanged(string? value) => CommitField("Artist", f => f with { Artist = Normalize(value) });

    partial void OnAlbumChanged(string? value) => CommitField("Album", f => f with { Album = Normalize(value) });

    partial void OnAlbumArtistChanged(string? value) => CommitField("Album Artist", f => f with { AlbumArtist = Normalize(value) });

    partial void OnGenreChanged(string? value) => CommitField("Genre", f => f with { Genre = Normalize(value) });

    partial void OnCommentChanged(string? value) => CommitField("Comment", f => f with { Comment = Normalize(value) });

    partial void OnComposerChanged(string? value) => CommitField("Composer", f => f with { Composer = Normalize(value) });

    partial void OnYearChanged(int? value) => CommitField("Year", f => f with { Year = value });

    partial void OnTrackNumberChanged(int? value) => CommitField("Track #", f => f with { TrackNumber = value });

    partial void OnDiscNumberChanged(int? value) => CommitField("Disc #", f => f with { DiscNumber = value });

    /// <summary>Builds a single-file <see cref="FieldEditCommand"/> (wrapped in a one-child
    /// <see cref="CompositeEditCommand"/>, matching <see cref="EditPanelViewModel"/>'s own
    /// convention of never pushing a bare leaf command directly) and pushes it via
    /// <see cref="EditHistory.Execute"/> — this alone both records the undo step and (via
    /// MainWindowViewModel's subscription to EditHistory.Changed) triggers an immediate
    /// auto-save, with no separate save step needed here.</summary>
    private void CommitField(string label, Func<TagFieldSet, TagFieldSet> mutate)
    {
        if (_isSyncingFromAudioFile)
            return;

        var before = AudioFile.PendingFields;
        var after = mutate(before);
        if (after == before)
            return;

        var description = $"Set {label} on {AudioFile.FileName}";
        _editHistory.Execute(new CompositeEditCommand(
            description,
            new IEditCommand[] { new FieldEditCommand(AudioFile, before, after, description) }));
    }

    private static string? Normalize(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private void OnAudioFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AudioFile.PendingFields):
                SyncFieldsFromAudioFile();
                OnPropertyChanged(nameof(IsDirty));
                break;

            case nameof(AudioFile.FileName):
                OnPropertyChanged(nameof(FileName));
                break;

            case nameof(AudioFile.IsDirty):
                OnPropertyChanged(nameof(IsDirty));
                break;
        }
    }

    /// <summary>Repopulates every field from the current <see cref="AudioFile.PendingFields"/>
    /// snapshot — called on construction and whenever it changes underneath this row (an edit
    /// made via the side panel for this same file, or an undo/redo). Guarded by
    /// <see cref="_isSyncingFromAudioFile"/> so this never turns around and commits a redundant
    /// (or worse, actively wrong) edit back into EditHistory.</summary>
    private void SyncFieldsFromAudioFile()
    {
        _isSyncingFromAudioFile = true;
        try
        {
            var fields = AudioFile.PendingFields;
            Title = fields.Title;
            Artist = fields.Artist;
            Album = fields.Album;
            AlbumArtist = fields.AlbumArtist;
            Genre = fields.Genre;
            Comment = fields.Comment;
            Composer = fields.Composer;
            Year = fields.Year;
            TrackNumber = fields.TrackNumber;
            DiscNumber = fields.DiscNumber;
        }
        finally
        {
            _isSyncingFromAudioFile = false;
        }
    }
}
