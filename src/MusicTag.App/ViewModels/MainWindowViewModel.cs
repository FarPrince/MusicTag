using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicTag.App.Services;
using MusicTag.Core.History;
using MusicTag.Core.Models;
using MusicTag.Core.Services;
using MusicTag.Core.Settings;

namespace MusicTag.App.ViewModels;

/// <summary>
/// M2/M3/M4/M5 scope: multi-selection editing + Ctrl+S save-all-dirty + Ctrl+Z/Ctrl+Y
/// undo/redo + F5 refresh + inline grid rename (<see cref="RenameFileInline"/>) via
/// RenameCommand/TryExecute. <see cref="SelectedFiles"/> (populated by the grid's
/// <see cref="Behaviors.DataGridSelectedItemsBehavior"/>, since DataGrid.SelectedItems isn't a
/// bindable DependencyProperty) replaced the M2-M4 single-selection <c>SelectedItem</c>
/// property — <see cref="EditPanel"/> now always reacts to the full selection (0/1/N files)
/// rather than a single item, per plan section 5. M8 adds the toolbar/menu's quick backdrop-toggle
/// (<see cref="ToggleBackdrop"/>) and the Help menu's "Keyboard Shortcuts" entry
/// (<see cref="ShowShortcuts"/>).
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IFolderScanService _folderScanService;
    private readonly IFilePickerService _filePickerService;
    private readonly IAudioFileService _audioFileService;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly EditHistory _editHistory;
    private readonly SemaphoreSlim _autoSaveGate = new(1, 1);

    public MainWindowViewModel(
        IFolderScanService folderScanService,
        IFilePickerService filePickerService,
        IAudioFileService audioFileService,
        IDialogService dialogService,
        ISettingsService settingsService,
        EditHistory editHistory,
        EditPanelViewModel editPanel,
        AlbumArtViewModel albumArt)
    {
        _folderScanService = folderScanService;
        _filePickerService = filePickerService;
        _audioFileService = audioFileService;
        _dialogService = dialogService;
        _settingsService = settingsService;
        _editHistory = editHistory;
        EditPanel = editPanel;
        AlbumArt = albumArt;

        _editHistory.Changed += OnEditHistoryChanged;
        SelectedFiles.CollectionChanged += OnSelectedFilesChanged;
        Files.CollectionChanged += (_, _) => NormalizeSeparatorsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Single instance shared for the life of the app (registered as a DI
    /// singleton) — its SetSelection is driven by <see cref="OnSelectedFilesChanged"/>.</summary>
    public EditPanelViewModel EditPanel { get; }

    /// <summary>M6: sibling of <see cref="EditPanel"/> (see MainWindow.xaml's right column),
    /// also driven by <see cref="OnSelectedFilesChanged"/> alongside it — same shared-singleton
    /// treatment.</summary>
    public AlbumArtViewModel AlbumArt { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private string? currentFolderPath;

    /// <summary>M5: mirrors the grid's live multi-selection — populated by
    /// <see cref="Behaviors.DataGridSelectedItemsBehavior"/> (bound as its
    /// <c>SelectedItems</c> attached property in MainWindow.xaml), since
    /// <c>DataGrid.SelectedItems</c> itself isn't a bindable DependencyProperty. Replaces the
    /// M2-M4 single-item <c>SelectedItem</c> property entirely — 0/1/N selected are all just
    /// different sizes of this same collection now.</summary>
    public ObservableCollection<FileListItemViewModel> SelectedFiles { get; } = [];

    /// <summary>Count of files with unsaved edits in the currently open folder, shown in
    /// the status bar. Recomputed whenever any file's IsDirty changes.</summary>
    [ObservableProperty]
    private int pendingChangesCount;

    /// <summary>Description of the command a Ctrl+Z would currently undo, or null when the
    /// undo stack is empty — mirrors <see cref="EditHistory.TopUndoDescription"/>, refreshed
    /// from <see cref="OnEditHistoryChanged"/>, shown in the status bar per plan section 5.</summary>
    [ObservableProperty]
    private string? undoRedoDescription;

    public ObservableCollection<FileListItemViewModel> Files { get; } = [];

    /// <summary>Grid column-chooser state (right-click ANY column header — see MainWindow.xaml.cs
    /// OnFileGridPreviewMouseRightButtonUp). Per user feedback, the chooser originally only
    /// covered the 5 columns hidden by default (Album Artist/Genre/Composer/Comment/Disc #) —
    /// there was no way to hide one of the columns shown by default (e.g. Track #). Every data
    /// column except Filename (the row's identity — hiding it would leave no way to tell rows
    /// apart) is now toggleable through the same menu. The values set here are just XAML-declared
    /// defaults for a brand-new settings file — MainWindow.xaml.cs's RestoreGridColumnState
    /// overwrites them (before first paint) from AppSettings.GridColumns when a prior session
    /// saved one, per user request that column selection persist across sessions.</summary>
    [ObservableProperty]
    private bool isTitleColumnVisible = true;

    [ObservableProperty]
    private bool isArtistColumnVisible = true;

    [ObservableProperty]
    private bool isAlbumColumnVisible = true;

    [ObservableProperty]
    private bool isTrackNumberColumnVisible = true;

    [ObservableProperty]
    private bool isYearColumnVisible = true;

    [ObservableProperty]
    private bool isDurationColumnVisible = true;

    [ObservableProperty]
    private bool isAlbumArtistColumnVisible;

    [ObservableProperty]
    private bool isGenreColumnVisible;

    [ObservableProperty]
    private bool isComposerColumnVisible;

    [ObservableProperty]
    private bool isCommentColumnVisible;

    [ObservableProperty]
    private bool isDiscNumberColumnVisible;

    /// <summary>Technical-metadata columns (per user feedback: "let me select technical info
    /// onto the main viewport as headers") — same hidden-by-default treatment as Album
    /// Artist/Genre/Composer/Comment/Disc # above.</summary>
    [ObservableProperty]
    private bool isCodecColumnVisible;

    [ObservableProperty]
    private bool isBitrateColumnVisible;

    [ObservableProperty]
    private bool isSampleRateColumnVisible;

    [ObservableProperty]
    private bool isChannelsColumnVisible;

    [ObservableProperty]
    private bool isFileSizeColumnVisible;

    [ObservableProperty]
    private bool isTagFormatsColumnVisible;

    [ObservableProperty]
    private bool isModifiedColumnVisible;

    [RelayCommand]
    private async Task OpenFolder()
    {
        var folder = _filePickerService.PickFolder(CurrentFolderPath);
        if (folder is null)
            return;

        // Opening a different folder discards the old AudioFile instances just like Refresh
        // does — same discard-confirmation + EditHistory.Clear() requirement per plan
        // section 4 ("old commands hold direct references to AudioFile instances that get
        // discarded/replaced on rescan").
        if (!ConfirmDiscardIfDirty())
            return;

        await LoadFolderAsync(folder);
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task Refresh()
    {
        if (CurrentFolderPath is null)
            return;

        if (!ConfirmDiscardIfDirty())
            return;

        await LoadFolderAsync(CurrentFolderPath);
    }

    private bool CanRefresh() => CurrentFolderPath is not null && Directory.Exists(CurrentFolderPath);

    /// <summary>M7: opens the modal Settings window (default startup folder, theme choice,
    /// Explorer-integration toggle). No discard-confirmation dance here — Settings doesn't
    /// touch the currently open folder's files at all.</summary>
    [RelayCommand]
    private void OpenSettings() => _dialogService.ShowSettings();

    /// <summary>M8: Help menu → "Keyboard Shortcuts" — shows the static reference window.</summary>
    [RelayCommand]
    private void ShowShortcuts() => _dialogService.ShowShortcutsReference();

    /// <summary>Toolbar "Normalize Separators" button, per user request: replaces ";"
    /// separators with ", " across every currently loaded file in the folder (per the user's
    /// own scope choice — not just the grid selection, unlike EditPanelViewModel's batch field
    /// edits), restricted to whichever fields <see cref="AppSettings.SeparatorNormalizationFields"/>
    /// currently enables. Builds one <see cref="FieldEditCommand"/> per file the transform would
    /// actually change (skipping files with no ';' to normalize in an enabled field) and wraps
    /// them in a single <see cref="CompositeEditCommand"/> — one undo step for the whole folder,
    /// same shape as every other batch edit in this app — which in turn triggers the usual
    /// auto-save via <see cref="OnEditHistoryChanged"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanNormalizeSeparators))]
    private void NormalizeSeparators()
    {
        var enabledFields = _settingsService.Load().SeparatorNormalizationFields;

        var leaves = new List<IEditCommand>();
        foreach (var item in Files)
        {
            var file = item.AudioFile;
            var before = file.PendingFields;
            var after = SeparatorNormalization.Apply(before, enabledFields);
            if (after == before)
                continue;

            leaves.Add(new FieldEditCommand(file, before, after, $"Normalize separators on {file.FileName}"));
        }

        if (leaves.Count == 0)
            return;

        var description = leaves.Count == 1
            ? leaves[0].Description
            : $"Normalize separators on {leaves.Count} files";

        _editHistory.Execute(new CompositeEditCommand(description, leaves));
    }

    private bool CanNormalizeSeparators() => Files.Count > 0;

    /// <summary>Toolbar "Search Lyrics" button, per user request: scans every directory
    /// configured under Settings → Lyrics (LRCLib) — recursively, independent of whatever
    /// folder happens to be open in the grid — for supported audio files with no sidecar .lrc
    /// yet, looks each one up on LRCLib (tags first, falling back to parsing "Artist - Title"
    /// out of the filename when tags are missing), and writes a synced .lrc next to every song
    /// that gets a match. See <see cref="LyricsSearchService"/> for the full per-file logic;
    /// the actual search run and its live progress/results popup are owned by
    /// <see cref="Services.IDialogService.ShowLyricsSearchDialog"/> /
    /// <see cref="LyricsSearchDialogViewModel"/> — this method is just validating there's
    /// something configured to search before opening it.</summary>
    [RelayCommand]
    private void SearchLyrics()
    {
        var directories = _settingsService.Load().LyricsSearchDirectories;
        if (directories.Count == 0)
        {
            _dialogService.ShowInfo(
                "Search Lyrics",
                "No directories configured yet — add at least one under Settings → Lyrics (LRCLib).");
            return;
        }

        _dialogService.ShowLyricsSearchDialog(directories);
    }

    /// <summary>M7: called once from App.OnStartup when a startup folder was resolved from a
    /// command-line arg (an Explorer-triggered launch) or the configured
    /// AppSettings.DefaultStartupFolder — see plan section 7's precedence rules. Deliberately
    /// skips <see cref="ConfirmDiscardIfDirty"/> since nothing is loaded yet at startup for
    /// there to be anything to discard; just delegates straight to the same LoadFolder every
    /// other folder-open path funnels through.</summary>
    public Task LoadInitialFolder(string folderPath) => LoadFolderAsync(folderPath);

    /// <summary>Ctrl+Z. Per plan section 4, ordinary field/album-art commands are pure
    /// in-memory and TryUndo cannot actually fail for them — the fallible Try* API only
    /// matters for RenameCommand (M4), at which point a failed Undo (e.g. a collision with a
    /// file created/renamed since) surfaces via RenameErrorDialog instead of corrupting the
    /// stacks or silently no-op'ing.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (!_editHistory.TryUndo(out var error))
        {
            _dialogService.ShowRenameError(error!.Message);
        }
    }

    private bool CanUndo() => _editHistory.CanUndo;

    /// <summary>Ctrl+Y — see <see cref="Undo"/> doc comment for the same TryRedo caveat.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (!_editHistory.TryRedo(out var error))
        {
            _dialogService.ShowRenameError(error!.Message);
        }
    }

    private bool CanRedo() => _editHistory.CanRedo;

    /// <summary>Driven by the grid's Filename column commit (Enter/focus-lost on the inline
    /// editor — see MainWindow.xaml's CellEditEnding handler). Builds a <see cref="RenameCommand"/>
    /// and pushes it via <see cref="EditHistory.TryExecute"/> (never the plain Execute — this
    /// is the one command type that performs real disk I/O and can fail). On failure, shows
    /// <see cref="IDialogService.ShowRenameError"/>; the Filename cell needs no separate
    /// "revert" step since a failed <see cref="IAudioFileService.Rename"/> call never mutates
    /// <see cref="MusicTag.Core.Models.AudioFile.FileName"/> — the grid cell (bound to it) is
    /// already showing the unchanged name.</summary>
    public void RenameFileInline(FileListItemViewModel item, string newFileName)
    {
        var file = item.AudioFile;
        var before = file.FileName;

        // Blank text (user cleared the cell) or an unchanged name: nothing to do, and
        // critically nothing to push onto the undo stack — mirrors EditPanelViewModel's
        // before==after no-op guard for ordinary field commits.
        if (string.IsNullOrWhiteSpace(newFileName) || string.Equals(newFileName, before, StringComparison.Ordinal))
            return;

        var command = new RenameCommand(_audioFileService, file, before, newFileName);

        if (!_editHistory.TryExecute(command, out var error))
        {
            _dialogService.ShowRenameError(error!.Message);
        }
    }

    /// <summary>Per plan section 4: Refresh/folder-open must check for any dirty file first
    /// and show a discard-confirmation dialog before proceeding; returns true immediately
    /// (no dialog) when nothing is dirty.</summary>
    private bool ConfirmDiscardIfDirty()
    {
        if (!Files.Any(f => f.AudioFile.IsDirty))
            return true;

        return _dialogService.ConfirmDiscardChanges();
    }

    /// <summary>Every commit (field edit, album-art edit, rename, undo, or redo) fires
    /// EditHistory.Changed — per user feedback, edits should never require a manual Ctrl+S,
    /// so every one of those also triggers an immediate background save of whatever is
    /// currently dirty. Renames already hit disk immediately on their own (see RenameCommand),
    /// so this only ever finds real work to do after a field/album-art commit or an undo/redo
    /// of one; it's a harmless no-op (zero dirty files) otherwise.</summary>
    private async void OnEditHistoryChanged(object? sender, EventArgs e)
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        UndoRedoDescription = _editHistory.TopUndoDescription;

        await AutoSaveDirtyFilesAsync();
    }

    /// <summary>Ctrl+S / the toolbar Save button: still available as an explicit "retry now"
    /// action (e.g. if an earlier auto-save failed because a file was locked), but normal
    /// editing never requires it — every commit already triggers this same save via
    /// <see cref="OnEditHistoryChanged"/>. Routed through the same gate so a manual Save can
    /// never run concurrently with an in-flight auto-save.</summary>
    [RelayCommand]
    private Task SaveAllAsync() => AutoSaveDirtyFilesAsync();

    /// <summary>Saves every dirty file in the currently open folder — not just the selection —
    /// matching Mp3tag semantics per the plan. Gated by a semaphore so rapid-fire commits (e.g.
    /// tabbing quickly through several grid cells) queue their saves sequentially instead of
    /// racing to open the same file for writing at once; each queued call re-reads the current
    /// dirty set rather than acting on a stale snapshot, so nothing is lost or double-saved.</summary>
    private async Task AutoSaveDirtyFilesAsync()
    {
        await _autoSaveGate.WaitAsync();
        try
        {
            var dirtyFiles = Files.Select(f => f.AudioFile).Where(f => f.IsDirty).ToList();
            if (dirtyFiles.Count == 0)
                return;

            var result = await _audioFileService.SaveManyAsync(dirtyFiles);

            if (result.Failed.Count > 0)
            {
                _dialogService.ShowSaveErrors(result.Failed);
            }
        }
        finally
        {
            _autoSaveGate.Release();
        }
    }

    /// <summary>Fires for every add/remove the grid's SelectionChanged produces (including
    /// the wholesale clear that happens when <see cref="LoadFolder"/> resets
    /// <see cref="SelectedFiles"/> directly) — always rebuilds EditPanel's bound state from
    /// the full current selection rather than trying to diff it incrementally.</summary>
    private void OnSelectedFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var files = SelectedFiles.Select(f => f.AudioFile).ToList();
        EditPanel.SetSelection(files);
        AlbumArt.SetSelection(files);
    }

    private async Task LoadFolderAsync(string folderPath)
    {
        CurrentFolderPath = folderPath;

        // Old FileListItemViewModel instances hold a live subscription on their AudioFile
        // (for grid-column live-update) and on themselves (for the status bar's dirty
        // count) — unhook both before discarding them so a rescan doesn't leave dangling
        // handlers pointing at AudioFile instances the grid no longer shows.
        foreach (var item in Files)
        {
            item.PropertyChanged -= OnFileItemPropertyChanged;
            item.Dispose();
        }

        Files.Clear();
        // ObservableCollection.Clear() always raises CollectionChanged (Reset), so this alone
        // is enough to drive EditPanel.SetSelection([]) via OnSelectedFilesChanged — no
        // separate explicit call needed.
        SelectedFiles.Clear();

        // Per plan section 4: old commands on the stack hold direct references to AudioFile
        // instances that this rescan is about to discard/replace, so the whole history must
        // be cleared here — the caller (OpenFolder/Refresh) has already confirmed discarding
        // any dirty state via ConfirmDiscardIfDirty before reaching this point. Note this is
        // the one thing that DOES clear EditHistory — Save deliberately does not (see
        // SaveAllAsync): undo-after-save is intentionally allowed and re-dirties the file.
        //
        // Deliberately AFTER Files.Clear() above, not before: EditHistory.Clear() fires
        // Changed synchronously, which (via OnEditHistoryChanged) immediately kicks off an
        // auto-save of whatever's currently dirty. If Files still held the old,
        // about-to-be-discarded items at that point, switching folders right after the user
        // confirmed "discard changes" would silently re-save the very edits they just chose
        // to discard. With Files already empty, that auto-save sees zero dirty files and is
        // a true no-op.
        _editHistory.Clear();

        IReadOnlyList<AudioFile> scannedFiles;
        try
        {
            // FolderScanService.ScanFolder only tolerates a single bad FILE (its own
            // try/catch is scoped per-file) — Directory.EnumerateFiles' lazy enumeration
            // itself can still throw (a network share dropping mid-scan, a permission-denied
            // folder), which isn't caught there. Task.Run also keeps the scan (every file is
            // fully tag-parsed) off the UI thread, so opening a large folder doesn't freeze
            // the window while it's in progress.
            scannedFiles = await Task.Run(() => _folderScanService.ScanFolder(folderPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogService.ShowError("Couldn't Open Folder", $"Couldn't read \"{folderPath}\":\n{ex.Message}");
            RecomputePendingChangesCount();
            return;
        }

        foreach (var audioFile in scannedFiles)
        {
            var item = new FileListItemViewModel(audioFile, _editHistory);
            item.PropertyChanged += OnFileItemPropertyChanged;
            Files.Add(item);
        }

        RecomputePendingChangesCount();
    }

    private void OnFileItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileListItemViewModel.IsDirty))
        {
            RecomputePendingChangesCount();
        }
    }

    private void RecomputePendingChangesCount()
        => PendingChangesCount = Files.Count(f => f.IsDirty);
}
