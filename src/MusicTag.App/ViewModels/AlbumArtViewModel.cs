using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicTag.App.Services;
using MusicTag.Core.History;
using MusicTag.Core.Models;
using MusicTag.Core.Services;

namespace MusicTag.App.ViewModels;

/// <summary>
/// M6 scope, per plan sections 3-5: owns album-art display + editing for the current grid
/// selection, independent of <see cref="EditPanelViewModel"/> (a sibling in the right-hand
/// column — see MainWindow.xaml — not nested inside it; M5 had temporarily embedded a
/// read-only <see cref="Controls.AlbumArtControl"/> directly in EditPanelView as a stand-in,
/// flagged there as "M6 scope"). Driven by the same <see cref="SetSelection"/> call site as
/// EditPanelViewModel (<c>MainWindowViewModel.OnSelectedFilesChanged</c>).
///
/// "Effective art" for a file is computed per plan section 5's byte-equality comparison:
/// - <see cref="AlbumArtAction.Replaced"/> -> that edit's NewImageBytes (not yet on disk).
/// - <see cref="AlbumArtAction.Removed"/> -> null (no art).
/// - <see cref="AlbumArtAction.Unchanged"/> -> whatever's currently embedded on disk, read via
///   <see cref="IAudioFileService.LoadEmbeddedAlbumArt"/> (the on-demand read path M2 added and
///   flagged as an open question for M6 to revisit — resolved here by keeping it on-demand
///   rather than caching on AudioFile, matching AudioFile's already-approved shape, which has
///   no "current art" slot per plan section 3).
///
/// Single selection is the trivial one-element case of the same distinctness computation used
/// for N-selection (mirrors EditPanelViewModel's "0/1/N are one code path" precedent from M5).
/// </summary>
public sealed partial class AlbumArtViewModel : ObservableObject
{
    private readonly IAudioFileService _audioFileService;
    private readonly IFilePickerService _filePickerService;
    private readonly EditHistory _editHistory;
    private IReadOnlyList<AudioFile> _selectedFiles = Array.Empty<AudioFile>();

    public AlbumArtViewModel(IAudioFileService audioFileService, IFilePickerService filePickerService, EditHistory editHistory)
    {
        _audioFileService = audioFileService;
        _filePickerService = filePickerService;
        _editHistory = editHistory;
    }

    /// <summary>True whenever at least one file is selected — drives whether Replace/Remove
    /// are enabled and whether the control shows its "nothing selected" blank state.</summary>
    [ObservableProperty]
    private bool isEnabled;

    /// <summary>The common album art across the whole selection, or null when the selection
    /// is empty, has no art, or (per <see cref="IsMixed"/>) disagrees on what art it has. Never
    /// partially populated — either every selected file's effective art matches this exact byte
    /// sequence, or this is null and <see cref="IsMixed"/> explains why.</summary>
    [NotifyCanExecuteChangedFor(nameof(ExtractCommand))]
    [ObservableProperty]
    private byte[]? imageBytes;

    /// <summary>True when more than one file is selected and their effective album art (see
    /// class doc comment) isn't byte-identical across all of them — drives
    /// AlbumArtControl's "multiple different images" placeholder state, per plan section 5.
    /// Always false for 0 or 1 selected files (trivially unanimous).</summary>
    [NotifyCanExecuteChangedFor(nameof(ExtractCommand))]
    [ObservableProperty]
    private bool isMixed;

    /// <summary>Per user feedback ("show album art details — image type, size, resolution,
    /// etc"): a one-line summary of the currently-displayed art (format/dimensions/byte size),
    /// or null whenever there's nothing to summarize (no art, or a mixed selection) — refreshed
    /// alongside <see cref="ImageBytes"/>/<see cref="IsMixed"/> in <see cref="RefreshArt"/>.</summary>
    [ObservableProperty]
    private string? artDetails;

    /// <summary>Called by <c>MainWindowViewModel</c> whenever the grid's selection changes —
    /// same call site/cadence as <see cref="EditPanelViewModel.SetSelection"/>.</summary>
    public void SetSelection(IReadOnlyList<AudioFile> files)
    {
        foreach (var file in _selectedFiles)
            file.PropertyChanged -= OnSelectedFilePropertyChanged;

        _selectedFiles = files;

        foreach (var file in _selectedFiles)
            file.PropertyChanged += OnSelectedFilePropertyChanged;

        IsEnabled = _selectedFiles.Count > 0;
        RefreshArt();

        ReplaceCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Reacts to any selected file's PendingAlbumArt changing out from under this view
    /// model — an Undo/Redo of a previous album-art edit, or (in a mixed selection) another
    /// file's edit changing whether the selection now agrees — so the preview/mixed-state
    /// stays in sync rather than going stale, mirroring EditPanelViewModel's
    /// OnSelectedFilePropertyChanged precedent.</summary>
    private void OnSelectedFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioFile.PendingAlbumArt))
            RefreshArt();
    }

    private void RefreshArt()
    {
        if (_selectedFiles.Count == 0)
        {
            ImageBytes = null;
            IsMixed = false;
            return;
        }

        var effectiveArts = _selectedFiles.Select(GetEffectiveArt).ToList();
        var first = effectiveArts[0];
        var allMatch = effectiveArts.All(art => BytesEqual(art, first));

        IsMixed = !allMatch;
        ImageBytes = allMatch ? first : null;
        ArtDetails = allMatch ? BuildArtDetails(first) : null;
    }

    private byte[]? GetEffectiveArt(AudioFile file) => file.PendingAlbumArt.Action switch
    {
        AlbumArtAction.Replaced => file.PendingAlbumArt.NewImageBytes,
        AlbumArtAction.Removed => null,
        _ => _audioFileService.LoadEmbeddedAlbumArt(file.FullPath),
    };

    private static bool BytesEqual(byte[]? a, byte[]? b)
    {
        if (a is null || b is null)
            return a is null && b is null;

        return a.AsSpan().SequenceEqual(b);
    }

    /// <summary>Opens a file picker and, if the user chose an image, applies it via
    /// <see cref="ApplyImageBytes"/> — the same all-or-nothing batch-apply path used by
    /// paste/drag-drop.</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Replace()
    {
        var path = _filePickerService.PickImageFile();
        if (path is null)
            return;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            // An unreadable/vanished-since-picked file: nothing sensible to apply. No
            // dedicated error dialog for this edge case per M6 scope — mirrors
            // AlbumArtControl's own "corrupt image data falls back silently" precedent
            // from M2 rather than introducing a new IDialogService method for it.
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        ApplyImageBytes(bytes);
    }

    /// <summary>Removes album art from the entire current selection as one all-or-nothing
    /// batch, per plan section 5 ("Replace/Remove apply to the entire current selection as one
    /// all-or-nothing CompositeEditCommand") — deliberately different from
    /// EditPanelViewModel.CommitField's "skip files that wouldn't change" nuance: every
    /// selected file gets a leaf AlbumArtEditCommand regardless of whether it already has no
    /// art, because this is a single all-or-nothing action, not N independent field edits.</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Remove() => ApplyToSelection(new AlbumArtEdit(AlbumArtAction.Removed, null), BuildDescription("Remove"));

    /// <summary>Shared apply path for Replace (file-picker), clipboard paste
    /// (<see cref="Behaviors.ClipboardPasteImageBehavior"/>), and drag-and-drop — all funnel
    /// through here so every input source gets the same all-or-nothing batch-apply
    /// behavior.</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ApplyImageBytes(byte[] bytes) => ApplyToSelection(new AlbumArtEdit(AlbumArtAction.Replaced, bytes), BuildDescription("Replace"));

    private bool CanEdit() => _selectedFiles.Count > 0;

    /// <summary>Per user feedback ("provide an option to extract album art as an image") —
    /// saves the currently-displayed art out to a standalone file. Disabled whenever there's
    /// nothing sensible to save (no art, or a mixed selection whose files disagree) — see the
    /// <see cref="NotifyCanExecuteChangedFor"/> attributes on <see cref="ImageBytes"/>/
    /// <see cref="IsMixed"/> above for why this stays in sync automatically rather than needing
    /// its own explicit refresh call.</summary>
    [RelayCommand(CanExecute = nameof(CanExtract))]
    private void Extract()
    {
        if (ImageBytes is not { Length: > 0 } bytes)
            return;

        var extension = SniffImageFormat(bytes).Extension;
        var suggestedName = (_selectedFiles.Count == 1
            ? Path.GetFileNameWithoutExtension(_selectedFiles[0].FileName)
            : "cover") + extension;

        var path = _filePickerService.PickSaveImageFile(suggestedName);
        if (path is null)
            return;

        try
        {
            File.WriteAllBytes(path, bytes);
        }
        catch (IOException)
        {
            // Same silent-failure stance as Replace's own unreadable-file catch above — no
            // dedicated error dialog for this edge case per the existing M6 precedent.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private bool CanExtract() => !IsMixed && ImageBytes is { Length: > 0 };

    /// <summary>Per user feedback ("show album art details — image type, size, resolution,
    /// etc"). Dimensions come from decoding just enough of the image to read its header (no
    /// full-frame render needed) — falls back to format/size alone if decoding fails (e.g. a
    /// truncated or otherwise malformed embedded image, which <see cref="Controls.AlbumArtControl"/>
    /// already tolerates by falling back to its placeholder).</summary>
    private static string BuildArtDetails(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
            return string.Empty;

        var format = SniffImageFormat(bytes);
        var sizeLabel = FormatFileSize(bytes.Length);

        try
        {
            using var stream = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            return $"{format.Label} · {frame.PixelWidth}×{frame.PixelHeight} · {sizeLabel}";
        }
        catch (Exception)
        {
            return $"{format.Label} · {sizeLabel}";
        }
    }

    /// <summary>Identifies an image's format from its magic-number header bytes rather than
    /// trusting any file extension (there isn't one yet — this is raw embedded-tag data), used
    /// by both <see cref="BuildArtDetails"/> (display label) and <see cref="Extract"/> (default
    /// save extension). Unrecognized data defaults to PNG — the format
    /// <see cref="Behaviors.ClipboardPasteImageBehavior"/> always re-encodes clipboard pastes
    /// as, so it's the most likely fallback in practice.</summary>
    private static (string Label, string Extension) SniffImageFormat(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return ("JPEG", ".jpg");

        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return ("PNG", ".png");

        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
            return ("BMP", ".bmp");

        if (bytes.Length >= 3 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return ("GIF", ".gif");

        return ("PNG", ".png");
    }

    private static string FormatFileSize(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        return bytes >= mb ? $"{bytes / mb:0.0} MB" : $"{bytes / kb:0.0} KB";
    }

    /// <summary>Builds one <see cref="AlbumArtEditCommand"/> per currently selected file
    /// (unconditionally — see <see cref="Remove"/>'s doc comment on why this never skips
    /// already-matching files, unlike EditPanelViewModel.CommitField) and pushes them as a
    /// single <see cref="CompositeEditCommand"/> via one <see cref="EditHistory.Execute"/>
    /// call, so the whole batch is one undo step and applies as one atomic unit.</summary>
    private void ApplyToSelection(AlbumArtEdit after, string description)
    {
        if (_selectedFiles.Count == 0)
            return;

        var leaves = _selectedFiles
            .Select(file => (IEditCommand)new AlbumArtEditCommand(file, file.PendingAlbumArt, after, description))
            .ToList();

        _editHistory.Execute(new CompositeEditCommand(description, leaves));
    }

    private string BuildDescription(string verb)
        => _selectedFiles.Count == 1
            ? $"{verb} album art on {_selectedFiles[0].FileName}"
            : $"{verb} album art on {_selectedFiles.Count} files";
}
