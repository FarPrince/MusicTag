using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicTag.App.ViewModels;
using MusicTag.Core.Settings;
using Wpf.Ui.Controls;

namespace MusicTag.App.Views;

/// <summary>
/// Code-behind is intentionally thin — all state and behavior live in
/// <see cref="MainWindowViewModel"/>, injected via DI (see App.xaml.cs) so the view model
/// stays unit-testable without a WPF host. The grid cell-editing handlers exist only because
/// inline DataGrid cell editing (the Filename column's F2/double-click rename, per plan
/// section 5) has no clean command-binding hook in WPF — everything they do is a one-line
/// delegation into <see cref="MainWindowViewModel"/>. Window-placement capture/restore (M7,
/// plan section 6) is the other genuinely view-level concern here: <c>Left</c>/<c>Top</c>/
/// <c>Width</c>/<c>Height</c>/<c>WindowState</c> and <see cref="SystemParameters"/> are WPF
/// Window properties with no sensible view-model equivalent, so this lives here rather than in
/// MainWindowViewModel — same reasoning as the grid handlers. M8 adds
/// <see cref="BeginRenameCommand"/>/<see cref="SelectAllFilesCommand"/>, the two shortcuts
/// (F2, Ctrl+A) that need to invoke a real <see cref="DataGrid"/> instance method
/// (<c>BeginEdit</c>/<c>SelectAll</c>) with no view-model-friendly command surface of their
/// own — see MainWindow.xaml's <c>Window.CommandBindings</c> doc comment for why these are
/// RoutedCommands rather than plain KeyDown handlers.
/// </summary>
public partial class MainWindow : FluentWindow
{
    /// <summary>F2 — begins inline edit on the Filename cell of the single selected row (see
    /// <see cref="OnBeginRenameExecuted"/>). Disabled (see
    /// <see cref="OnBeginRenameCanExecute"/>) unless exactly one file is selected, mirroring
    /// the grid's own inline-rename being a per-row, single-file action (plan section 5:
    /// "Filename is excluded from [the multi-select edit panel] entirely... renaming stays
    /// exclusively an inline per-row grid action").</summary>
    public static readonly RoutedCommand BeginRenameCommand = new(nameof(BeginRenameCommand), typeof(MainWindow));

    /// <summary>Ctrl+A — selects every row currently in the file grid (see
    /// <see cref="OnSelectAllFilesExecuted"/>).</summary>
    public static readonly RoutedCommand SelectAllFilesCommand = new(nameof(SelectAllFilesCommand), typeof(MainWindow));

    private readonly MainWindowViewModel _viewModel;
    private readonly ISettingsService _settingsService;

    public MainWindow(MainWindowViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = viewModel;

        RestoreWindowPlacement();
        RestoreGridColumnState();
        CaptureCanonicalColumnWidths();
        FileGrid.SizeChanged += OnFileGridSizeChanged;
        Closing += OnClosing;

        // DataGridColumn derives from plain DependencyObject, not FrameworkElement — it has no
        // DataContext of its own and a {Binding} on DataGridColumn.Visibility silently fails to
        // resolve (confirmed empirically: the columns rendered despite defaulting to false), so
        // the column-chooser's show/hide state is applied directly here instead.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyAllOptionalColumnVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.IsTitleColumnVisible):
                TitleColumn.Visibility = ToVisibility(_viewModel.IsTitleColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsArtistColumnVisible):
                ArtistColumn.Visibility = ToVisibility(_viewModel.IsArtistColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsAlbumColumnVisible):
                AlbumColumn.Visibility = ToVisibility(_viewModel.IsAlbumColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsTrackNumberColumnVisible):
                TrackNumberColumn.Visibility = ToVisibility(_viewModel.IsTrackNumberColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsYearColumnVisible):
                YearColumn.Visibility = ToVisibility(_viewModel.IsYearColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsDurationColumnVisible):
                DurationColumn.Visibility = ToVisibility(_viewModel.IsDurationColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsAlbumArtistColumnVisible):
                AlbumArtistColumn.Visibility = ToVisibility(_viewModel.IsAlbumArtistColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsGenreColumnVisible):
                GenreColumn.Visibility = ToVisibility(_viewModel.IsGenreColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsComposerColumnVisible):
                ComposerColumn.Visibility = ToVisibility(_viewModel.IsComposerColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsCommentColumnVisible):
                CommentColumn.Visibility = ToVisibility(_viewModel.IsCommentColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsDiscNumberColumnVisible):
                DiscNumberColumn.Visibility = ToVisibility(_viewModel.IsDiscNumberColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsCodecColumnVisible):
                CodecColumn.Visibility = ToVisibility(_viewModel.IsCodecColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsBitrateColumnVisible):
                BitrateColumn.Visibility = ToVisibility(_viewModel.IsBitrateColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsSampleRateColumnVisible):
                SampleRateColumn.Visibility = ToVisibility(_viewModel.IsSampleRateColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsChannelsColumnVisible):
                ChannelsColumn.Visibility = ToVisibility(_viewModel.IsChannelsColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsFileSizeColumnVisible):
                FileSizeColumn.Visibility = ToVisibility(_viewModel.IsFileSizeColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsTagFormatsColumnVisible):
                TagFormatsColumn.Visibility = ToVisibility(_viewModel.IsTagFormatsColumnVisible);
                break;
            case nameof(MainWindowViewModel.IsModifiedColumnVisible):
                ModifiedColumn.Visibility = ToVisibility(_viewModel.IsModifiedColumnVisible);
                break;
        }
    }

    private void ApplyAllOptionalColumnVisibility()
    {
        TitleColumn.Visibility = ToVisibility(_viewModel.IsTitleColumnVisible);
        ArtistColumn.Visibility = ToVisibility(_viewModel.IsArtistColumnVisible);
        AlbumColumn.Visibility = ToVisibility(_viewModel.IsAlbumColumnVisible);
        TrackNumberColumn.Visibility = ToVisibility(_viewModel.IsTrackNumberColumnVisible);
        YearColumn.Visibility = ToVisibility(_viewModel.IsYearColumnVisible);
        DurationColumn.Visibility = ToVisibility(_viewModel.IsDurationColumnVisible);
        AlbumArtistColumn.Visibility = ToVisibility(_viewModel.IsAlbumArtistColumnVisible);
        GenreColumn.Visibility = ToVisibility(_viewModel.IsGenreColumnVisible);
        ComposerColumn.Visibility = ToVisibility(_viewModel.IsComposerColumnVisible);
        CommentColumn.Visibility = ToVisibility(_viewModel.IsCommentColumnVisible);
        DiscNumberColumn.Visibility = ToVisibility(_viewModel.IsDiscNumberColumnVisible);
        CodecColumn.Visibility = ToVisibility(_viewModel.IsCodecColumnVisible);
        BitrateColumn.Visibility = ToVisibility(_viewModel.IsBitrateColumnVisible);
        SampleRateColumn.Visibility = ToVisibility(_viewModel.IsSampleRateColumnVisible);
        ChannelsColumn.Visibility = ToVisibility(_viewModel.IsChannelsColumnVisible);
        FileSizeColumn.Visibility = ToVisibility(_viewModel.IsFileSizeColumnVisible);
        TagFormatsColumn.Visibility = ToVisibility(_viewModel.IsTagFormatsColumnVisible);
        ModifiedColumn.Visibility = ToVisibility(_viewModel.IsModifiedColumnVisible);
    }

    private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Single source of truth for "which DataGridColumn goes with which settings key
    /// and which MainWindowViewModel visibility property" — shared by
    /// <see cref="RestoreGridColumnState"/> and <see cref="CaptureGridColumnState"/> so the two
    /// directions of this mapping can't drift apart. FilenameColumn has no visibility toggle
    /// (it's the row's identity — see the column-chooser's own doc comment), hence the null
    /// getter/setter for it alone.</summary>
    private (string Name, DataGridColumn Column, Func<bool>? GetVisible, Action<bool>? SetVisible)[] GetGridColumnBindings() =>
    [
        ("FilenameColumn", FilenameColumn, null, null),
        ("TitleColumn", TitleColumn, () => _viewModel.IsTitleColumnVisible, v => _viewModel.IsTitleColumnVisible = v),
        ("ArtistColumn", ArtistColumn, () => _viewModel.IsArtistColumnVisible, v => _viewModel.IsArtistColumnVisible = v),
        ("AlbumColumn", AlbumColumn, () => _viewModel.IsAlbumColumnVisible, v => _viewModel.IsAlbumColumnVisible = v),
        ("TrackNumberColumn", TrackNumberColumn, () => _viewModel.IsTrackNumberColumnVisible, v => _viewModel.IsTrackNumberColumnVisible = v),
        ("YearColumn", YearColumn, () => _viewModel.IsYearColumnVisible, v => _viewModel.IsYearColumnVisible = v),
        ("DurationColumn", DurationColumn, () => _viewModel.IsDurationColumnVisible, v => _viewModel.IsDurationColumnVisible = v),
        ("AlbumArtistColumn", AlbumArtistColumn, () => _viewModel.IsAlbumArtistColumnVisible, v => _viewModel.IsAlbumArtistColumnVisible = v),
        ("GenreColumn", GenreColumn, () => _viewModel.IsGenreColumnVisible, v => _viewModel.IsGenreColumnVisible = v),
        ("ComposerColumn", ComposerColumn, () => _viewModel.IsComposerColumnVisible, v => _viewModel.IsComposerColumnVisible = v),
        ("CommentColumn", CommentColumn, () => _viewModel.IsCommentColumnVisible, v => _viewModel.IsCommentColumnVisible = v),
        ("DiscNumberColumn", DiscNumberColumn, () => _viewModel.IsDiscNumberColumnVisible, v => _viewModel.IsDiscNumberColumnVisible = v),
        ("CodecColumn", CodecColumn, () => _viewModel.IsCodecColumnVisible, v => _viewModel.IsCodecColumnVisible = v),
        ("BitrateColumn", BitrateColumn, () => _viewModel.IsBitrateColumnVisible, v => _viewModel.IsBitrateColumnVisible = v),
        ("SampleRateColumn", SampleRateColumn, () => _viewModel.IsSampleRateColumnVisible, v => _viewModel.IsSampleRateColumnVisible = v),
        ("ChannelsColumn", ChannelsColumn, () => _viewModel.IsChannelsColumnVisible, v => _viewModel.IsChannelsColumnVisible = v),
        ("FileSizeColumn", FileSizeColumn, () => _viewModel.IsFileSizeColumnVisible, v => _viewModel.IsFileSizeColumnVisible = v),
        ("TagFormatsColumn", TagFormatsColumn, () => _viewModel.IsTagFormatsColumnVisible, v => _viewModel.IsTagFormatsColumnVisible = v),
        ("ModifiedColumn", ModifiedColumn, () => _viewModel.IsModifiedColumnVisible, v => _viewModel.IsModifiedColumnVisible = v),
    ];

    /// <summary>Applied once, before <see cref="ApplyAllOptionalColumnVisibility"/> — overwrites
    /// each column's XAML-declared default Width, DisplayIndex, and each MainWindowViewModel
    /// visibility property from whatever a prior session last saved (see
    /// <see cref="CaptureGridColumnState"/>), per user request that "the tags I selected in the
    /// headers and the width of each tag" — and, per a follow-up request, the order the user
    /// placed them in — persist across sessions. Does nothing on first-ever run (no grid state
    /// saved yet, empty dictionary), leaving the XAML/ViewModel defaults in effect.</summary>
    private void RestoreGridColumnState()
    {
        var saved = _settingsService.Load().GridColumns;
        if (saved.Count == 0)
        {
            return;
        }

        var bindings = GetGridColumnBindings();
        foreach (var (name, column, _, setVisible) in bindings)
        {
            if (!saved.TryGetValue(name, out var state))
            {
                continue;
            }

            setVisible?.Invoke(state.Visible);

            // A null/unrecognized WidthUnitType means either a settings file saved before this
            // field existed, or one that's otherwise unreliable — see GridColumnState's own doc
            // comment on why that must NOT fall back to treating the raw Width as a pixel value:
            // that's exactly the bug that shipped in v1.6 (every Star-sized column silently
            // frozen to a fixed pixel width, breaking window-resize scaling). Leaving Width
            // untouched here keeps the XAML-declared default (Star/Auto) in effect instead.
            //
            // A Star value <= 0 is never valid (a real bug in an earlier version of the
            // resize-cascade guard could write one out — see MinStarValue's own doc comment) —
            // restoring it would keep that column permanently zero/negative-width every session
            // from then on, so this is treated the same as an unrecognized unit type: fall back
            // to the XAML default instead of trusting corrupt data.
            if (state.WidthUnitType is { } unitTypeName
                && Enum.TryParse<DataGridLengthUnitType>(unitTypeName, out var unitType)
                && (unitType != DataGridLengthUnitType.Star || state.Width > 0))
            {
                column.Width = new DataGridLength(state.Width, unitType);
            }
        }

        // Reordering is separate from the width/visibility loop above because DisplayIndex
        // assignments interact with every other column's DisplayIndex (WPF shifts the rest of
        // the grid to keep indices a contiguous 0..N-1 permutation), so every column has to be
        // assigned together in one ascending pass rather than one at a time as each is
        // encountered. Only runs at all once every column in the grid has a real saved index —
        // a settings file written before this feature existed (or a partial/corrupt one) has
        // every DisplayIndex defaulted to -1 (see GridColumnState's own doc comment), and
        // reordering off of a subset would misplace the columns with no saved position instead
        // of just leaving the whole arrangement as XAML declared it.
        var ordered = bindings
            .Where(b => saved.TryGetValue(b.Name, out var state) && state.DisplayIndex >= 0)
            .Select(b => (b.Column, Index: saved[b.Name].DisplayIndex))
            .OrderBy(entry => entry.Index)
            .ToList();

        if (ordered.Count != bindings.Length)
        {
            return;
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Column.DisplayIndex = i;
        }
    }

    /// <summary>Captures every column's current Width verbatim — Value AND UnitType (see
    /// <see cref="GridColumnState"/>'s own doc comment on why ActualWidth alone isn't enough) —
    /// current visibility, and current DisplayIndex (the user's drag-to-reorder position),
    /// called from <see cref="OnClosing"/> alongside window-placement capture.</summary>
    private Dictionary<string, GridColumnState> CaptureGridColumnState()
    {
        var result = new Dictionary<string, GridColumnState>();

        foreach (var (name, column, getVisible, _) in GetGridColumnBindings())
        {
            // _canonicalColumnWidths (kept in sync by OnFileGridSizeChanged) is trusted over
            // the column's own live Width — see that dictionary's own doc comment on why the
            // live value alone can't be trusted after a resize.
            var width = _canonicalColumnWidths.TryGetValue(column, out var canonicalWidth) ? canonicalWidth : column.Width;

            result[name] = new GridColumnState(
                getVisible?.Invoke() ?? true,
                width.Value,
                column.DisplayIndex,
                width.UnitType.ToString());
        }

        return result;
    }

    /// <summary>Applied once, before <c>Show()</c> (see App.xaml.cs) — restores the last
    /// captured position/size/maximized-state, clamped to the *current* virtual screen bounds
    /// so a since-removed/reconfigured monitor can never strand the window off-screen
    /// (plan section 6). Does nothing on first-ever run (no placement saved yet), leaving the
    /// XAML-declared defaults (Height=600, Width=1000, WindowStartupLocation=CenterScreen) in
    /// effect.</summary>
    private void RestoreWindowPlacement()
    {
        var placement = _settingsService.Load().LastWindowPlacement;
        if (placement is null)
        {
            return;
        }

        var clamped = ClampToVirtualScreen(placement);

        // Left/Top/Width/Height first, then WindowState — WPF resolves RestoreBounds from
        // whatever these were set to just before a maximize, so this order matters if
        // IsMaximized is true.
        Left = clamped.Left;
        Top = clamped.Top;
        Width = clamped.Width;
        Height = clamped.Height;

        if (clamped.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>Captures the current position/size/maximized-state on window close and
    /// persists it via <see cref="ISettingsService"/> (reload-then-mutate, so a Settings-window
    /// save that happened earlier in the same session isn't clobbered). Best-effort: a failure
    /// to save here must never block the app from actually closing.</summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        try
        {
            var settings = _settingsService.Load();
            settings.LastWindowPlacement = CaptureWindowPlacement();
            settings.GridColumns = CaptureGridColumnState();
            _settingsService.Save(settings);
        }
        catch (Exception)
        {
            // Never let a settings-save failure prevent the window (and app) from closing.
        }
    }

    private WindowPlacement CaptureWindowPlacement()
    {
        // While maximized, Left/Top/Width/Height report the full-screen bounds, not the
        // window's "normal" bounds — RestoreBounds is what to persist instead, so restoring
        // later (with IsMaximized re-applied) gives back a sensible normal-state size too.
        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        return new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState == WindowState.Maximized);
    }

    private static WindowPlacement ClampToVirtualScreen(WindowPlacement placement)
    {
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualWidth = SystemParameters.VirtualScreenWidth;
        var virtualHeight = SystemParameters.VirtualScreenHeight;

        var width = Math.Min(placement.Width, virtualWidth);
        var height = Math.Min(placement.Height, virtualHeight);

        var left = Math.Max(virtualLeft, Math.Min(placement.Left, virtualLeft + virtualWidth - width));
        var top = Math.Max(virtualTop, Math.Min(placement.Top, virtualTop + virtualHeight - height));

        return placement with { Left = left, Top = top, Width = width, Height = height };
    }

    /// <summary>Fires on Enter, Tab, or focus-lost (commit) as well as Escape (cancel) for
    /// EVERY column's cell — not just Filename — since DataGrid.CellEditEnding is a
    /// grid-level event. Real bug caught by manual testing: this originally called
    /// <see cref="MainWindowViewModel.RenameFileInline"/> unconditionally for any commit,
    /// which meant committing an edit in, say, the Title column also renamed the file to
    /// whatever text was typed there (Title and Filename happened to end up identical, since
    /// both were reading the same "last edited TextBox" content) — confirmed by watching the
    /// actual file on disk get renamed to a Title value with no extension. Only the Filename
    /// column's own edit should ever trigger a rename; every other column already commits its
    /// value entirely through its own two-way Binding (see FileListItemViewModel's settable
    /// properties) with no code-behind involvement needed at all, so this must return
    /// immediately for anything other than <see cref="FilenameColumn"/>. The editing TextBox's
    /// Text binding for Filename specifically is explicitly OneWay (see MainWindow.xaml), so
    /// nothing here relies on WPF's own edit-commit write-back for that column; the typed text
    /// is read straight off the element instead.</summary>
    private void OnFileGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
            return;

        if (!ReferenceEquals(e.Column, FilenameColumn))
            return;

        // Real bug caught by manual testing: for a DataGridTemplateColumn (Filename),
        // e.EditingElement is the ContentPresenter hosting the CellEditingTemplate — NOT the
        // TextBox declared inside it — so a direct "is TextBox" check always failed here and
        // silently no-op'd every commit (confirmed via a temporary diagnostic log: typing and
        // Tab-ing correctly reached and updated the TextBox's own Text, but this handler never
        // even got as far as calling RenameFileInline because the type check missed). Plain
        // DataGridTextColumn columns don't have this problem since WPF generates their editing
        // TextBox directly as EditingElement with no wrapping presenter — this is specific to
        // template columns. Walking the visual tree for the actual TextBox handles both shapes.
        if (e.EditingElement is not DependencyObject editingElement || FindTextBox(editingElement) is not { } textBox)
            return;

        if (e.Row.Item is not FileListItemViewModel item)
            return;

        _viewModel.RenameFileInline(item, textBox.Text);
    }

    /// <summary>Depth-first search for the first <see cref="System.Windows.Controls.TextBox"/>
    /// in <paramref name="root"/>'s visual subtree (or <paramref name="root"/> itself, if it
    /// already is one) — see <see cref="OnFileGridCellEditEnding"/>'s doc comment for why a
    /// direct type check on <c>DataGridCellEditEndingEventArgs.EditingElement</c> isn't enough
    /// for a <see cref="DataGridTemplateColumn"/>.</summary>
    private static System.Windows.Controls.TextBox? FindTextBox(DependencyObject root)
    {
        if (root is System.Windows.Controls.TextBox textBox)
            return textBox;

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            if (FindTextBox(VisualTreeHelper.GetChild(root, i)) is { } found)
                return found;
        }

        return null;
    }

    /// <summary>Real bug caught by manual testing: unlike a plain <see cref="DataGridTextColumn"/>
    /// (Title/Artist/etc.), which gets double-click-to-edit for free, <see cref="DataGridTemplateColumn"/>
    /// (Filename) does NOT enter edit mode on double-click out of the box — a well-known WPF
    /// limitation, confirmed here by watching the automation tree for an edit control that
    /// simply never appeared after a real double-click. F2 already worked (it calls
    /// <see cref="DataGrid.BeginEdit()"/> programmatically — see <see cref="OnBeginRenameExecuted"/>),
    /// but double-click silently doing nothing on the one column most people would actually try
    /// it on first is exactly the bug reported ("editing filename doesn't save" — because
    /// there's nothing to commit if edit mode was never entered). Handled by watching for a
    /// genuine double-click landing on a non-editing Filename cell and calling BeginEdit
    /// explicitly, the same call F2 already makes. One more nuance found while fixing this:
    /// this fires during the PREVIEW (tunneling) phase, before WPF has necessarily updated
    /// <see cref="DataGrid.CurrentCell"/> for the click that's still in progress — calling
    /// <c>BeginEdit()</c> without first pointing <c>CurrentCell</c> explicitly at the clicked
    /// row/column risked silently editing whatever cell was left "current" from a previous
    /// interaction instead (confirmed: an initial version of this fix that skipped the explicit
    /// <c>CurrentCell</c> assignment still showed no edit control appearing).</summary>
    private void OnFileGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            TryBeginColumnResizeCascadeGuard(e);
        }

        if (e.ClickCount != 2)
            return;

        if (FindAncestor<System.Windows.Controls.DataGridCell>(e.OriginalSource as DependencyObject) is not { } cell)
            return;

        if (!ReferenceEquals(cell.Column, FilenameColumn) || cell.IsEditing)
            return;

        if (FindAncestor<System.Windows.Controls.DataGridRow>(cell) is not { } row)
            return;

        FileGrid.CurrentCell = new DataGridCellInfo(row.Item, FilenameColumn);
        FileGrid.BeginEdit(e);
        e.Handled = true;
    }

    private readonly Dictionary<DataGridColumn, double> _preResizeStarValues = new();
    private DataGridColumn? _resizingStarColumn;
    private DataGridColumn? _resizingStarNeighbor;
    private DataGridColumn? _resizingColumn;

    /// <summary>Every column's "real" Width (Value AND UnitType together), as last set by
    /// either <see cref="RestoreGridColumnState"/> or a completed manual resize (see
    /// <see cref="OnFileGridPreviewMouseLeftButtonUp"/>) — NOT necessarily whatever
    /// <c>column.Width</c> reports live at any given moment. Reported back twice: first for
    /// Star columns (shrinking the window and returning it to its original size didn't restore
    /// the original per-column proportions), then again for a Pixel column specifically
    /// (manually resizing Duration down to its MinWidth, then shrinking and widening the window,
    /// left it several times larger than the set minimum) — so this covers every column, not
    /// just Star ones. WPF's own DataGrid column-width algorithm, once it has had to clamp one
    /// or more columns during a resize, does not reliably recover every column's pre-clamp
    /// Width when space is freed up again, Star or Pixel. Rather than chase the exact internal
    /// WPF mechanics of when/why that happens, <see cref="OnFileGridSizeChanged"/> treats this
    /// dictionary as authoritative and re-asserts it onto every tracked column on every
    /// layout-affecting size change, so whatever WPF's own algorithm silently drifted a column
    /// to gets corrected back within the same layout pass — self-healing regardless of the
    /// precise trigger or column type.</summary>
    private readonly Dictionary<DataGridColumn, DataGridLength> _canonicalColumnWidths = new();
    private bool _isReassertingColumnWidths;

    /// <summary>WPF's default DataGrid column resize redistributes a Star column's width
    /// change across EVERY other Star column proportionally (documented WPF behavior — "if the
    /// column being resized doesn't have enough room, other star columns are resized in
    /// proportion") rather than confining the change to just the two columns sharing the
    /// dragged border — reported back as "moving the handle for one column shouldn't impact
    /// the width of others (only the tags directly adjacent to the handle)". A Star column's
    /// Value (the coefficient — e.g. 2 for "2*") never changes on its own from a window
    /// resize; only ActualWidth (the rendered pixel size) does — Value only ever changes from
    /// a manual header-border drag. That makes "snapshot every Star column's Value right as a
    /// resize-grip mouse-down happens, then reset every column except the dragged one and its
    /// immediate neighbor back to that snapshot once the drag ends" (see
    /// <see cref="OnFileGridPreviewMouseLeftButtonUp"/>) a safe, targeted fix: it can't
    /// misfire on a plain window resize, since that path never touches Value at all. Every
    /// mouse-down clears any stale pending state first — a resize started but never properly
    /// finished (focus lost mid-drag, etc.) must not have its guard fire later against an
    /// unrelated mouse-up.</summary>
    private void TryBeginColumnResizeCascadeGuard(MouseButtonEventArgs e)
    {
        _resizingStarColumn = null;
        _resizingStarNeighbor = null;
        _resizingColumn = null;

        if (FindAncestor<System.Windows.Controls.Primitives.DataGridColumnHeader>(e.OriginalSource as DependencyObject) is not { Column: { } column } header)
            return;

        // The resize grip lives at the header's right edge — a plain click elsewhere on the
        // header (e.g. starting a reorder-drag, or a click that lands on a sort indicator)
        // must not arm the guard.
        const double edgeTolerance = 6.0;
        if (header.ActualWidth - e.GetPosition(header).X > edgeTolerance)
            return;

        // Tracked regardless of column type — OnFileGridPreviewMouseLeftButtonUp uses this to
        // refresh _canonicalColumnWidths even for a Pixel/Auto column, which needs no cascade
        // correction (only Star columns cascade) but still needs its new width remembered so
        // OnFileGridSizeChanged doesn't fight the user's intentional resize on the next layout
        // pass.
        _resizingColumn = column;

        if (column.Width.UnitType != DataGridLengthUnitType.Star)
            return;

        var starColumns = FileGrid.Columns
            .Where(c => c.Visibility == Visibility.Visible && c.Width.UnitType == DataGridLengthUnitType.Star)
            .OrderBy(c => c.DisplayIndex)
            .ToList();

        var index = starColumns.IndexOf(column);
        if (index < 0)
            return;

        // The dragged column's own right border trades with whichever visible Star column is
        // next in display order — falling back to the previous one if this is the last
        // visible Star column (nothing to its right to trade with).
        var neighbor = index < starColumns.Count - 1 ? starColumns[index + 1] : index > 0 ? starColumns[index - 1] : null;
        if (neighbor is null)
            return;

        _resizingStarColumn = column;
        _resizingStarNeighbor = neighbor;
        _preResizeStarValues.Clear();
        foreach (var starColumn in starColumns)
        {
            _preResizeStarValues[starColumn] = starColumn.Width.Value;
        }
    }

    /// <summary>Floor for a Star column's coefficient after a manual resize — prevents the
    /// neighbor-compensation math in <see cref="OnFileGridPreviewMouseLeftButtonUp"/> from ever
    /// producing a zero or negative Star value. Real bug caught in the wild: dragging a column's
    /// border far enough that the requested change exceeded the neighbor's entire pre-drag
    /// value drove the neighbor's stored Value negative (observed as -1.616 in a real saved
    /// settings file), which is a nonsensical DataGridLength and corrupts that column going
    /// forward. Small enough to never visibly affect an ordinary resize.</summary>
    private const double MinStarValue = 0.05;

    /// <summary>Completes whatever <see cref="TryBeginColumnResizeCascadeGuard"/> armed. For a
    /// Star column: redistributes exactly the dragged pair's combined pre-drag Star budget
    /// between the two of them (clamped so neither goes below <see cref="MinStarValue"/> — see
    /// its own doc comment), and puts every other Star column back exactly where it was before
    /// the drag, undoing whatever WPF's default proportional redistribution did to them. For a
    /// Pixel/Auto column (no cascade risk — only Star columns cascade), just refreshes its
    /// canonical width to the new value the user just set. Either way, refreshing
    /// <see cref="_canonicalColumnWidths"/> for whatever changed is what keeps
    /// <see cref="OnFileGridSizeChanged"/> from fighting a genuine, intentional resize on the
    /// next layout pass. A no-op if no resize-edge mouse-down armed the guard.</summary>
    private void OnFileGridPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_resizingStarColumn is { } column && _resizingStarNeighbor is { } neighbor)
        {
            var totalPairValue = _preResizeStarValues[column] + _preResizeStarValues[neighbor];
            var requestedColumnValue = column.Width.Value;
            var clampedColumnValue = Math.Clamp(requestedColumnValue, MinStarValue, totalPairValue - MinStarValue);
            var clampedNeighborValue = totalPairValue - clampedColumnValue;

            column.Width = new DataGridLength(clampedColumnValue, DataGridLengthUnitType.Star);
            neighbor.Width = new DataGridLength(clampedNeighborValue, DataGridLengthUnitType.Star);

            foreach (var (starColumn, originalValue) in _preResizeStarValues)
            {
                if (ReferenceEquals(starColumn, column) || ReferenceEquals(starColumn, neighbor))
                    continue;

                starColumn.Width = new DataGridLength(originalValue, DataGridLengthUnitType.Star);
            }

            // Only the dragged pair's canonical width actually changed here — everyone else was
            // just reset back to the same canonical width they already had (see
            // _canonicalColumnWidths' own doc comment).
            _canonicalColumnWidths[column] = column.Width;
            _canonicalColumnWidths[neighbor] = neighbor.Width;
        }
        else if (_resizingColumn is { } plainColumn)
        {
            _canonicalColumnWidths[plainColumn] = plainColumn.Width;
        }

        _resizingStarColumn = null;
        _resizingStarNeighbor = null;
        _resizingColumn = null;
        _preResizeStarValues.Clear();
    }

    /// <summary>Snapshots every column's current Width into <see cref="_canonicalColumnWidths"/> —
    /// called once at startup, right after <see cref="RestoreGridColumnState"/> has applied
    /// whatever was saved (or left the XAML defaults in effect on a first-ever run), so the
    /// dictionary always has an authoritative baseline for <see cref="OnFileGridSizeChanged"/>
    /// to re-assert from.</summary>
    private void CaptureCanonicalColumnWidths()
    {
        foreach (var (_, column, _, _) in GetGridColumnBindings())
        {
            _canonicalColumnWidths[column] = column.Width;
        }
    }

    /// <summary>Fires on every layout-affecting size change to the grid itself — a window
    /// resize, a maximize/restore, or dragging the GridSplitter between the grid and the
    /// AlbumArt/EditPanel side panel — and re-asserts <see cref="_canonicalColumnWidths"/> onto
    /// every tracked column, undoing whatever WPF's own column-width algorithm silently drifted
    /// them to (see that dictionary's own doc comment for the reported symptoms this fixes).
    /// Guarded by <see cref="_isReassertingColumnWidths"/> since setting a column's Width here
    /// can itself trigger another SizeChanged before this one returns — without the guard, a
    /// genuine drift-correction would recurse into itself instead of just re-running once
    /// against already-correct values (a harmless, single extra pass at worst).</summary>
    private void OnFileGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isReassertingColumnWidths)
            return;

        _isReassertingColumnWidths = true;
        try
        {
            foreach (var (column, canonicalWidth) in _canonicalColumnWidths)
            {
                if (column.Width.Value != canonicalWidth.Value || column.Width.UnitType != canonicalWidth.UnitType)
                {
                    column.Width = canonicalWidth;
                }
            }
        }
        finally
        {
            _isReassertingColumnWidths = false;
        }
    }

    /// <summary>Auto-selects the existing filename text when the inline editor appears, the
    /// same "type to replace, or click to reposition the caret" UX Explorer/Mp3tag both use
    /// for rename — a small nicety, not called for by the plan's literal text but a natural
    /// fit for "F2 or double-click to edit."</summary>
    private void OnRenameTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        var textBox = (System.Windows.Controls.TextBox)sender;
        textBox.Focus();
        textBox.SelectAll();
    }

    /// <summary>Per user feedback ("enter should go to the next song below on the same
    /// metadata, tab should go to the next metadata right of the same song"): WPF's own default
    /// DataGrid key handling doesn't match this — Tab in particular moves to the *next row's
    /// first column* once it runs off the end of a row, rather than staying within the row. This
    /// takes over Enter/Tab entirely (both commit whatever's mid-edit first, then move) so
    /// behavior is consistent regardless of whether the current cell happens to be in edit mode
    /// or merely selected. Both then call <see cref="DataGrid.BeginEdit()"/> on the destination
    /// cell (skipping read-only columns) so a rapid batch-edit session — type a value, Enter/Tab,
    /// keep typing — never needs an extra double-click/F2 per cell.</summary>
    private void OnFileGridPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Tab)
            return;

        var currentCell = FileGrid.CurrentCell;
        if (currentCell.Column is null || currentCell.Item is null)
            return;

        FileGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        FileGrid.CommitEdit(DataGridEditingUnit.Row, true);

        if (e.Key == Key.Enter)
            MoveToNextRow(currentCell);
        else
            MoveToNextColumnInRow(currentCell, reverse: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));

        e.Handled = true;
    }

    /// <summary>Enter: same column, one row down — deliberately does NOT wrap past the last
    /// row (there's no "next song" to go to).</summary>
    private void MoveToNextRow(DataGridCellInfo currentCell)
    {
        var items = FileGrid.Items;
        var rowIndex = items.IndexOf(currentCell.Item);
        if (rowIndex < 0 || rowIndex >= items.Count - 1)
            return;

        FocusCell(items[rowIndex + 1], currentCell.Column);
    }

    /// <summary>Tab (Shift+Tab reverses): next (or previous) *visible* column in the same row,
    /// cycling back to the first (or last) column rather than spilling into another row — per
    /// user feedback, Tab should stay scoped to "the same song." Ordered by DisplayIndex rather
    /// than declaration order since CanUserReorderColumns defaults to true (a user may have
    /// dragged columns into a different order).</summary>
    private void MoveToNextColumnInRow(DataGridCellInfo currentCell, bool reverse)
    {
        var columns = FileGrid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .OrderBy(c => c.DisplayIndex)
            .ToList();

        var index = columns.IndexOf(currentCell.Column);
        if (index < 0 || columns.Count == 0)
            return;

        var nextIndex = reverse ? (index - 1 + columns.Count) % columns.Count : (index + 1) % columns.Count;
        FocusCell(currentCell.Item, columns[nextIndex]);
    }

    private void FocusCell(object item, DataGridColumn column)
    {
        FileGrid.CurrentCell = new DataGridCellInfo(item, column);
        FileGrid.SelectedItem = item;
        FileGrid.ScrollIntoView(item, column);
        FileGrid.Focus();

        if (!column.IsReadOnly)
            FileGrid.BeginEdit();
    }

    /// <summary>M8: F2 — only enabled when exactly one file is selected (a multi-selection has
    /// no single row to rename, and renaming zero rows is meaningless).</summary>
    private void OnBeginRenameCanExecute(object sender, CanExecuteRoutedEventArgs e)
        => e.CanExecute = _viewModel.SelectedFiles.Count == 1;

    /// <summary>M8: F2 — puts the single selected row's Filename cell into edit mode, the same
    /// state a double-click on that cell reaches. <see cref="DataGrid.BeginEdit()"/> has no
    /// command-bindable equivalent, hence this being a code-behind RoutedCommand handler
    /// rather than something MainWindowViewModel could do directly.</summary>
    private void OnBeginRenameExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (_viewModel.SelectedFiles.Count != 1)
            return;

        var item = _viewModel.SelectedFiles[0];
        FileGrid.CurrentCell = new DataGridCellInfo(item, FilenameColumn);
        FileGrid.Focus();
        FileGrid.BeginEdit();
    }

    /// <summary>M8: Ctrl+A — always available; selecting all rows of an empty grid is a
    /// harmless no-op.</summary>
    private void OnSelectAllFilesCanExecute(object sender, CanExecuteRoutedEventArgs e)
        => e.CanExecute = true;

    /// <summary>M8: Ctrl+A — selects every row in the file grid via
    /// <see cref="DataGrid.SelectAll()"/>, which raises the grid's native
    /// <c>SelectionChanged</c> and so flows into <c>MainWindowViewModel.SelectedFiles</c>
    /// through the existing one-directional <see cref="Behaviors.DataGridSelectedItemsBehavior"/>
    /// with no extra plumbing needed.</summary>
    private void OnSelectAllFilesExecuted(object sender, ExecutedRoutedEventArgs e) => FileGrid.SelectAll();

    /// <summary>The column-chooser the user asked for ("right click on headers to select which
    /// fields are seen"). Rather than restyling <c>DataGridColumnHeader</c> via
    /// <c>ColumnHeaderStyle</c> (which would require a <c>BasedOn</c> against whatever WPF-UI's
    /// actual default header style key is — not published/discoverable without decompiling the
    /// theme assembly, and getting it wrong risks silently losing the Fluent header chrome or a
    /// hard runtime resource-lookup failure), this builds the menu entirely in code and only
    /// opens it when the right-click actually landed on a column header (walking up from
    /// <see cref="RoutedEventArgs.OriginalSource"/> to look for a <see cref="DataGridColumnHeader"/>
    /// ancestor) — a right-click on a data row or empty grid space does nothing here, leaving
    /// room for a future row-context-menu without collision.
    ///
    /// Per user feedback, this originally only listed the 5 columns hidden by default, so there
    /// was no way to hide e.g. Track # — every column except Filename (the row's identity) is
    /// now listed, in the same left-to-right order they appear in the grid.</summary>
    private void OnFileGridPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<System.Windows.Controls.Primitives.DataGridColumnHeader>(e.OriginalSource as DependencyObject) is null)
            return;

        var menu = new System.Windows.Controls.ContextMenu();
        AddColumnToggleItem(menu, "Title", () => _viewModel.IsTitleColumnVisible, v => _viewModel.IsTitleColumnVisible = v);
        AddColumnToggleItem(menu, "Artist", () => _viewModel.IsArtistColumnVisible, v => _viewModel.IsArtistColumnVisible = v);
        AddColumnToggleItem(menu, "Album", () => _viewModel.IsAlbumColumnVisible, v => _viewModel.IsAlbumColumnVisible = v);
        AddColumnToggleItem(menu, "Track #", () => _viewModel.IsTrackNumberColumnVisible, v => _viewModel.IsTrackNumberColumnVisible = v);
        AddColumnToggleItem(menu, "Year", () => _viewModel.IsYearColumnVisible, v => _viewModel.IsYearColumnVisible = v);
        AddColumnToggleItem(menu, "Duration", () => _viewModel.IsDurationColumnVisible, v => _viewModel.IsDurationColumnVisible = v);
        AddColumnToggleItem(menu, "Album Artist", () => _viewModel.IsAlbumArtistColumnVisible, v => _viewModel.IsAlbumArtistColumnVisible = v);
        AddColumnToggleItem(menu, "Genre", () => _viewModel.IsGenreColumnVisible, v => _viewModel.IsGenreColumnVisible = v);
        AddColumnToggleItem(menu, "Composer", () => _viewModel.IsComposerColumnVisible, v => _viewModel.IsComposerColumnVisible = v);
        AddColumnToggleItem(menu, "Comment", () => _viewModel.IsCommentColumnVisible, v => _viewModel.IsCommentColumnVisible = v);
        AddColumnToggleItem(menu, "Disc #", () => _viewModel.IsDiscNumberColumnVisible, v => _viewModel.IsDiscNumberColumnVisible = v);
        menu.Items.Add(new System.Windows.Controls.Separator());
        AddColumnToggleItem(menu, "Codec", () => _viewModel.IsCodecColumnVisible, v => _viewModel.IsCodecColumnVisible = v);
        AddColumnToggleItem(menu, "Bitrate", () => _viewModel.IsBitrateColumnVisible, v => _viewModel.IsBitrateColumnVisible = v);
        AddColumnToggleItem(menu, "Sample Rate", () => _viewModel.IsSampleRateColumnVisible, v => _viewModel.IsSampleRateColumnVisible = v);
        AddColumnToggleItem(menu, "Channels", () => _viewModel.IsChannelsColumnVisible, v => _viewModel.IsChannelsColumnVisible = v);
        AddColumnToggleItem(menu, "File Size", () => _viewModel.IsFileSizeColumnVisible, v => _viewModel.IsFileSizeColumnVisible = v);
        AddColumnToggleItem(menu, "Tag Formats", () => _viewModel.IsTagFormatsColumnVisible, v => _viewModel.IsTagFormatsColumnVisible = v);
        AddColumnToggleItem(menu, "Modified", () => _viewModel.IsModifiedColumnVisible, v => _viewModel.IsModifiedColumnVisible = v);

        menu.PlacementTarget = FileGrid;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static void AddColumnToggleItem(
        System.Windows.Controls.ContextMenu menu, string header, Func<bool> getCurrent, Action<bool> setNew)
    {
        var item = new System.Windows.Controls.MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = getCurrent(),
        };
        item.Click += (_, _) => setNew(item.IsChecked);
        menu.Items.Add(item);
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T match)
                return match;

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
