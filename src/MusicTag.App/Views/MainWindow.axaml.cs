using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MusicTag.App.ViewModels;
using MusicTag.Core.Settings;

namespace MusicTag.App.Views;

/// <summary>
/// Code-behind is intentionally thin — all state and behavior live in
/// <see cref="MainWindowViewModel"/>, injected via DI (see App.axaml.cs) so the view model stays
/// unit-testable without an Avalonia host. The grid cell-editing handlers exist only because
/// inline DataGrid cell editing (the Filename column's F2/double-click rename) has no clean
/// command-binding hook — everything they do is a one-line delegation into
/// <see cref="MainWindowViewModel"/>. Window-placement capture/restore is the other genuinely
/// view-level concern here: <see cref="Window.Position"/>/<see cref="Layoutable.Width"/>/
/// <see cref="Layoutable.Height"/>/<see cref="Window.WindowState"/> have no sensible view-model
/// equivalent, so this lives here rather than in MainWindowViewModel — same reasoning as the
/// grid handlers.
///
/// Ported from the original WPF MainWindow.xaml.cs. Two pieces of that file's logic are
/// deliberately NOT carried over here, as a documented scope decision rather than an oversight:
/// <list type="bullet">
/// <item>The Star-column resize-cascade guard (WPF's DataGrid redistributes a dragged Star
/// column's width change proportionally across every other Star column, not just its immediate
/// neighbor — a real, specifically-WPF DataGrid behavior the original code compensated for).
/// Avalonia's DataGrid is a related but independently-evolved port with no confirmed equivalent
/// quirk; blindly porting a compensating workaround for a bug that may not exist here risked
/// introducing a new one instead of fixing a real one.</item>
/// <item>The double-click-to-edit workaround for <see cref="DataGridTemplateColumn"/> (a
/// documented WPF DataGrid limitation — template columns don't enter edit mode on double-click
/// out of the box). Avalonia's <c>DataGridCell</c> doesn't expose the public
/// Column/IsEditing surface the WPF workaround needed to detect "double-click landed on a
/// non-editing Filename cell" in the first place, and it's unconfirmed whether Avalonia's
/// DataGridTemplateColumn has the same limitation to begin with. F2 (see
/// <see cref="OnKeyDown"/>) remains a fully reliable way to start a rename regardless.</item>
/// </list>
/// </summary>
public partial class MainWindow : Window
{
    // DataGridColumn is a plain AvaloniaObject, not a StyledElement — Avalonia's x:Name field
    // generation (and NameScope registration) only applies to StyledElements, so unlike
    // FileGrid itself (a real Control), these columns can't be x:Name'd in MainWindow.axaml the
    // way the WPF original's DataGridColumns were. Each column instead carries its identity as
    // a plain string Tag (set in the XAML), resolved into these fields once here via
    // FindColumn — every other reference to e.g. TitleColumn throughout this file is otherwise
    // unchanged from the WPF original.
    private readonly DataGridColumn FilenameColumn;
    private readonly DataGridColumn TitleColumn;
    private readonly DataGridColumn ArtistColumn;
    private readonly DataGridColumn AlbumColumn;
    private readonly DataGridColumn TrackNumberColumn;
    private readonly DataGridColumn YearColumn;
    private readonly DataGridColumn DurationColumn;
    private readonly DataGridColumn AlbumArtistColumn;
    private readonly DataGridColumn GenreColumn;
    private readonly DataGridColumn ComposerColumn;
    private readonly DataGridColumn CommentColumn;
    private readonly DataGridColumn DiscNumberColumn;
    private readonly DataGridColumn CodecColumn;
    private readonly DataGridColumn BitrateColumn;
    private readonly DataGridColumn SampleRateColumn;
    private readonly DataGridColumn ChannelsColumn;
    private readonly DataGridColumn FileSizeColumn;
    private readonly DataGridColumn TagFormatsColumn;
    private readonly DataGridColumn ModifiedColumn;

    private readonly MainWindowViewModel _viewModel;
    private readonly ISettingsService _settingsService;

    public MainWindow(MainWindowViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();

        FilenameColumn = FindColumn("FilenameColumn");
        TitleColumn = FindColumn("TitleColumn");
        ArtistColumn = FindColumn("ArtistColumn");
        AlbumColumn = FindColumn("AlbumColumn");
        TrackNumberColumn = FindColumn("TrackNumberColumn");
        YearColumn = FindColumn("YearColumn");
        DurationColumn = FindColumn("DurationColumn");
        AlbumArtistColumn = FindColumn("AlbumArtistColumn");
        GenreColumn = FindColumn("GenreColumn");
        ComposerColumn = FindColumn("ComposerColumn");
        CommentColumn = FindColumn("CommentColumn");
        DiscNumberColumn = FindColumn("DiscNumberColumn");
        CodecColumn = FindColumn("CodecColumn");
        BitrateColumn = FindColumn("BitrateColumn");
        SampleRateColumn = FindColumn("SampleRateColumn");
        ChannelsColumn = FindColumn("ChannelsColumn");
        FileSizeColumn = FindColumn("FileSizeColumn");
        TagFormatsColumn = FindColumn("TagFormatsColumn");
        ModifiedColumn = FindColumn("ModifiedColumn");

        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = viewModel;

        RestoreWindowPlacement();
        RestoreGridColumnState();

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        Closing += OnClosing;

        // DataGridColumn isn't a Control with its own DataContext — a {Binding} on
        // DataGridColumn.IsVisible would have no source to resolve against, so the column-
        // chooser's show/hide state is applied directly here instead (same treatment the WPF
        // original gave this, for the same reason).
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyAllOptionalColumnVisibility();
    }

    private DataGridColumn FindColumn(string tag) => FileGrid.Columns.First(c => Equals(c.Tag, tag));

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.IsTitleColumnVisible):
                TitleColumn.IsVisible = _viewModel.IsTitleColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsArtistColumnVisible):
                ArtistColumn.IsVisible = _viewModel.IsArtistColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsAlbumColumnVisible):
                AlbumColumn.IsVisible = _viewModel.IsAlbumColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsTrackNumberColumnVisible):
                TrackNumberColumn.IsVisible = _viewModel.IsTrackNumberColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsYearColumnVisible):
                YearColumn.IsVisible = _viewModel.IsYearColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsDurationColumnVisible):
                DurationColumn.IsVisible = _viewModel.IsDurationColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsAlbumArtistColumnVisible):
                AlbumArtistColumn.IsVisible = _viewModel.IsAlbumArtistColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsGenreColumnVisible):
                GenreColumn.IsVisible = _viewModel.IsGenreColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsComposerColumnVisible):
                ComposerColumn.IsVisible = _viewModel.IsComposerColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsCommentColumnVisible):
                CommentColumn.IsVisible = _viewModel.IsCommentColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsDiscNumberColumnVisible):
                DiscNumberColumn.IsVisible = _viewModel.IsDiscNumberColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsCodecColumnVisible):
                CodecColumn.IsVisible = _viewModel.IsCodecColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsBitrateColumnVisible):
                BitrateColumn.IsVisible = _viewModel.IsBitrateColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsSampleRateColumnVisible):
                SampleRateColumn.IsVisible = _viewModel.IsSampleRateColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsChannelsColumnVisible):
                ChannelsColumn.IsVisible = _viewModel.IsChannelsColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsFileSizeColumnVisible):
                FileSizeColumn.IsVisible = _viewModel.IsFileSizeColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsTagFormatsColumnVisible):
                TagFormatsColumn.IsVisible = _viewModel.IsTagFormatsColumnVisible;
                break;
            case nameof(MainWindowViewModel.IsModifiedColumnVisible):
                ModifiedColumn.IsVisible = _viewModel.IsModifiedColumnVisible;
                break;
        }
    }

    private void ApplyAllOptionalColumnVisibility()
    {
        TitleColumn.IsVisible = _viewModel.IsTitleColumnVisible;
        ArtistColumn.IsVisible = _viewModel.IsArtistColumnVisible;
        AlbumColumn.IsVisible = _viewModel.IsAlbumColumnVisible;
        TrackNumberColumn.IsVisible = _viewModel.IsTrackNumberColumnVisible;
        YearColumn.IsVisible = _viewModel.IsYearColumnVisible;
        DurationColumn.IsVisible = _viewModel.IsDurationColumnVisible;
        AlbumArtistColumn.IsVisible = _viewModel.IsAlbumArtistColumnVisible;
        GenreColumn.IsVisible = _viewModel.IsGenreColumnVisible;
        ComposerColumn.IsVisible = _viewModel.IsComposerColumnVisible;
        CommentColumn.IsVisible = _viewModel.IsCommentColumnVisible;
        DiscNumberColumn.IsVisible = _viewModel.IsDiscNumberColumnVisible;
        CodecColumn.IsVisible = _viewModel.IsCodecColumnVisible;
        BitrateColumn.IsVisible = _viewModel.IsBitrateColumnVisible;
        SampleRateColumn.IsVisible = _viewModel.IsSampleRateColumnVisible;
        ChannelsColumn.IsVisible = _viewModel.IsChannelsColumnVisible;
        FileSizeColumn.IsVisible = _viewModel.IsFileSizeColumnVisible;
        TagFormatsColumn.IsVisible = _viewModel.IsTagFormatsColumnVisible;
        ModifiedColumn.IsVisible = _viewModel.IsModifiedColumnVisible;
    }

    /// <summary>Single source of truth for "which DataGridColumn goes with which settings key
    /// and which MainWindowViewModel visibility property" — shared by
    /// <see cref="RestoreGridColumnState"/> and <see cref="CaptureGridColumnState"/> so the two
    /// directions of this mapping can't drift apart. FilenameColumn has no visibility toggle
    /// (it's the row's identity), hence the null getter/setter for it alone.</summary>
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
    /// <see cref="CaptureGridColumnState"/>). Does nothing on first-ever run (no grid state
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
            // field existed, or one that's otherwise unreliable (see GridColumnState's own doc
            // comment) — leaving Width untouched here keeps the XAML-declared default (Star/
            // Auto) in effect instead of trusting corrupt/stale data. Same treatment for a
            // Width <= 0, which is never valid for any unit type this app actually uses.
            if (state.WidthUnitType is { } unitTypeName
                && Enum.TryParse<DataGridLengthUnitType>(unitTypeName, out var unitType)
                && state.Width > 0)
            {
                column.Width = new DataGridLength(state.Width, unitType);
            }
        }

        // Reordering is separate from the width/visibility loop above because DisplayIndex
        // assignments interact with every other column's DisplayIndex, so every column has to
        // be assigned together in one ascending pass rather than one at a time. Only runs at
        // all once every column in the grid has a real saved index — a settings file written
        // before this feature existed has every DisplayIndex defaulted to -1, and reordering
        // off of a subset would misplace the columns with no saved position.
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

    /// <summary>Captures every column's current Width (Value AND UnitType — see
    /// <see cref="GridColumnState"/>'s own doc comment on why ActualWidth alone isn't enough),
    /// current visibility, and current DisplayIndex (the user's drag-to-reorder position),
    /// called from <see cref="OnClosing"/> alongside window-placement capture.</summary>
    private Dictionary<string, GridColumnState> CaptureGridColumnState()
    {
        var result = new Dictionary<string, GridColumnState>();

        foreach (var (name, column, getVisible, _) in GetGridColumnBindings())
        {
            result[name] = new GridColumnState(
                getVisible?.Invoke() ?? true,
                column.Width.Value,
                column.DisplayIndex,
                column.Width.UnitType.ToString());
        }

        return result;
    }

    /// <summary>Applied once, before the window is shown — restores the last captured position/
    /// size/maximized-state, clamped to the primary screen's working area so a since-removed/
    /// reconfigured monitor can never strand the window off-screen. Does nothing on first-ever
    /// run (no placement saved yet), leaving the XAML-declared defaults (Height=600, Width=1000,
    /// WindowStartupLocation=CenterScreen) in effect.
    ///
    /// Deliberately clamps against only the primary screen's working area rather than the full
    /// multi-monitor virtual-desktop span the WPF original used (<c>SystemParameters
    /// .VirtualScreen*</c>) — Avalonia's <see cref="Window.Position"/> is in physical pixels
    /// while Width/Height are logical (DIP) units, and combining that with a precise multi-
    /// monitor union bounds calculation is meaningfully more complex for a "restore where the
    /// user left it" convenience feature than the safety it would add; clamping to the primary
    /// screen can never strand the window off-screen either way, just less precisely on an
    /// unchanged multi-monitor setup.</summary>
    private void RestoreWindowPlacement()
    {
        var placement = _settingsService.Load().LastWindowPlacement;
        if (placement is null)
        {
            return;
        }

        var screen = Screens.Primary;
        if (screen is null)
        {
            return;
        }

        // Screen.WorkingArea is physical pixels; Window.Position also wants physical pixels, but
        // Width/Height are logical (DIP) units — RenderScaling converts between the two spaces.
        var scaling = RenderScaling > 0 ? RenderScaling : 1.0;
        var workingArea = screen.WorkingArea;
        var workingAreaDip = new Rect(
            workingArea.X / scaling, workingArea.Y / scaling,
            workingArea.Width / scaling, workingArea.Height / scaling);

        var width = Math.Min(placement.Width, workingAreaDip.Width);
        var height = Math.Min(placement.Height, workingAreaDip.Height);
        var left = Math.Max(workingAreaDip.X, Math.Min(placement.Left, workingAreaDip.Right - width));
        var top = Math.Max(workingAreaDip.Y, Math.Min(placement.Top, workingAreaDip.Bottom - height));

        Width = width;
        Height = height;
        Position = new PixelPoint((int)(left * scaling), (int)(top * scaling));

        if (placement.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>Captures the current position/size/maximized-state on window close and persists
    /// it via <see cref="ISettingsService"/> (reload-then-mutate, so a Settings-window save that
    /// happened earlier in the same session isn't clobbered). Best-effort: a failure to save
    /// here must never block the app from actually closing.</summary>
    private void OnClosing(object? sender, WindowClosingEventArgs e)
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
        var scaling = RenderScaling > 0 ? RenderScaling : 1.0;
        return new WindowPlacement(Position.X / scaling, Position.Y / scaling, Width, Height, WindowState == WindowState.Maximized);
    }

    /// <summary>Fires on Enter, Tab, or focus-lost (commit) as well as Escape (cancel) for EVERY
    /// column's cell — not just Filename — since DataGrid.CellEditEnding is a grid-level event.
    /// Only the Filename column's own edit should ever trigger a rename; every other column
    /// already commits its value entirely through its own two-way Binding (see
    /// FileListItemViewModel's settable properties) with no code-behind involvement needed at
    /// all, so this must return immediately for anything other than <see cref="FilenameColumn"/>.
    /// The editing TextBox's Text binding for Filename specifically is explicitly OneWay (see
    /// MainWindow.axaml), so nothing here relies on the grid's own edit-commit write-back for
    /// that column; the typed text is read straight off the element instead.</summary>
    private async void OnFileGridCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
            return;

        if (!ReferenceEquals(e.Column, FilenameColumn))
            return;

        // For a DataGridTemplateColumn (Filename), EditingElement is the presenter hosting the
        // CellEditingTemplate, not the TextBox declared inside it — walking the visual tree for
        // the actual TextBox handles that regardless of exactly how the template is realized.
        if (FindDescendantTextBox(e.EditingElement) is not { } textBox)
            return;

        if (e.Row.DataContext is not FileListItemViewModel item)
            return;

        await _viewModel.RenameFileInlineAsync(item, textBox.Text ?? string.Empty);
    }

    private static TextBox? FindDescendantTextBox(Visual? root)
    {
        if (root is null)
            return null;

        if (root is TextBox textBox)
            return textBox;

        foreach (var child in root.GetVisualChildren())
        {
            if (FindDescendantTextBox(child) is { } found)
                return found;
        }

        return null;
    }

    /// <summary>Auto-selects the existing filename text when the inline editor appears, the
    /// same "type to replace, or click to reposition the caret" UX Explorer/Mp3tag both use for
    /// rename.</summary>
    private void OnRenameTextBoxLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        textBox.Focus();
        textBox.SelectAll();
    }

    /// <summary>Per user feedback ("enter should go to the next song below on the same
    /// metadata, tab should go to the next metadata right of the same song"): this takes over
    /// Enter/Tab entirely (both commit whatever's mid-edit first, then move) so behavior is
    /// consistent regardless of whether the current cell happens to be in edit mode or merely
    /// selected. Both then call <see cref="DataGrid.BeginEdit()"/> on the destination cell
    /// (skipping read-only columns) so a rapid batch-edit session never needs an extra double-
    /// click/F2 per cell.
    ///
    /// Also handles F2 (begin rename on the single selected row) and Ctrl+A (select every row) —
    /// ported from the WPF original's RoutedCommand/CommandBinding pair, simplified to a plain
    /// KeyDown handler since Avalonia's KeyBinding.Command needs no RoutedCommand indirection to
    /// reach code-behind the way WPF's did. Bubble-phase, at the Window level, so a TextBox's
    /// own built-in Ctrl+A (select-all-text) handling — which marks the event handled — is
    /// always seen first when a text field has focus, exactly mirroring the WPF original's
    /// "outward event resolution" scoping for the same shortcut.</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;

        if (e.Key == Key.F2)
        {
            if (_viewModel.SelectedFiles.Count != 1)
                return;

            var item = _viewModel.SelectedFiles[0];
            FileGrid.SelectedItem = item;
            FileGrid.CurrentColumn = FilenameColumn;
            FileGrid.Focus();
            FileGrid.BeginEdit();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.Control)
        {
            FileGrid.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter && e.Key != Key.Tab)
            return;

        var currentItem = FileGrid.SelectedItem;
        var currentColumn = FileGrid.CurrentColumn;
        if (currentItem is null || currentColumn is null)
            return;

        FileGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        FileGrid.CommitEdit(DataGridEditingUnit.Row, true);

        if (e.Key == Key.Enter)
            MoveToNextRow(currentItem, currentColumn);
        else
            MoveToNextColumnInRow(currentItem, currentColumn, reverse: e.KeyModifiers.HasFlag(KeyModifiers.Shift));

        e.Handled = true;
    }

    /// <summary>Enter: same column, one row down — deliberately does NOT wrap past the last row
    /// (there's no "next song" to go to).</summary>
    private void MoveToNextRow(object currentItem, DataGridColumn currentColumn)
    {
        if (currentItem is not FileListItemViewModel currentFileItem)
            return;

        var items = _viewModel.Files;
        var rowIndex = items.IndexOf(currentFileItem);
        if (rowIndex < 0 || rowIndex >= items.Count - 1)
            return;

        FocusCell(items[rowIndex + 1], currentColumn);
    }

    /// <summary>Tab (Shift+Tab reverses): next (or previous) *visible* column in the same row,
    /// cycling back to the first (or last) column rather than spilling into another row.
    /// Ordered by DisplayIndex rather than declaration order since a user may have dragged
    /// columns into a different order.</summary>
    private void MoveToNextColumnInRow(object currentItem, DataGridColumn currentColumn, bool reverse)
    {
        var columns = FileGrid.Columns
            .Where(c => c.IsVisible)
            .OrderBy(c => c.DisplayIndex)
            .ToList();

        var index = columns.IndexOf(currentColumn);
        if (index < 0 || columns.Count == 0)
            return;

        var nextIndex = reverse ? (index - 1 + columns.Count) % columns.Count : (index + 1) % columns.Count;
        FocusCell(currentItem, columns[nextIndex]);
    }

    private void FocusCell(object item, DataGridColumn column)
    {
        FileGrid.SelectedItem = item;
        FileGrid.CurrentColumn = column;
        FileGrid.ScrollIntoView(item, column);
        FileGrid.Focus();

        if (!column.IsReadOnly)
            FileGrid.BeginEdit();
    }

    /// <summary>The column-chooser the user asked for ("right click on headers to select which
    /// fields are seen"). Built entirely in code, and only opens when the right-click actually
    /// landed on a column header (walking up from the pointer event's source to look for a
    /// <see cref="DataGridColumnHeader"/> ancestor) — a right-click on a data row or empty grid
    /// space does nothing here. Every column except Filename (the row's identity) is listed, in
    /// the same left-to-right order they appear in the grid.</summary>
    private void OnFileGridPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right)
            return;

        if ((e.Source as Visual)?.FindAncestorOfType<DataGridColumnHeader>() is null)
            return;

        var menu = new MenuFlyout { Placement = PlacementMode.Pointer };
        var items = new List<Control>();
        AddColumnToggleItem(items, "Title", () => _viewModel.IsTitleColumnVisible, v => _viewModel.IsTitleColumnVisible = v);
        AddColumnToggleItem(items, "Artist", () => _viewModel.IsArtistColumnVisible, v => _viewModel.IsArtistColumnVisible = v);
        AddColumnToggleItem(items, "Album", () => _viewModel.IsAlbumColumnVisible, v => _viewModel.IsAlbumColumnVisible = v);
        AddColumnToggleItem(items, "Track #", () => _viewModel.IsTrackNumberColumnVisible, v => _viewModel.IsTrackNumberColumnVisible = v);
        AddColumnToggleItem(items, "Year", () => _viewModel.IsYearColumnVisible, v => _viewModel.IsYearColumnVisible = v);
        AddColumnToggleItem(items, "Duration", () => _viewModel.IsDurationColumnVisible, v => _viewModel.IsDurationColumnVisible = v);
        AddColumnToggleItem(items, "Album Artist", () => _viewModel.IsAlbumArtistColumnVisible, v => _viewModel.IsAlbumArtistColumnVisible = v);
        AddColumnToggleItem(items, "Genre", () => _viewModel.IsGenreColumnVisible, v => _viewModel.IsGenreColumnVisible = v);
        AddColumnToggleItem(items, "Composer", () => _viewModel.IsComposerColumnVisible, v => _viewModel.IsComposerColumnVisible = v);
        AddColumnToggleItem(items, "Comment", () => _viewModel.IsCommentColumnVisible, v => _viewModel.IsCommentColumnVisible = v);
        AddColumnToggleItem(items, "Disc #", () => _viewModel.IsDiscNumberColumnVisible, v => _viewModel.IsDiscNumberColumnVisible = v);
        items.Add(new Separator());
        AddColumnToggleItem(items, "Codec", () => _viewModel.IsCodecColumnVisible, v => _viewModel.IsCodecColumnVisible = v);
        AddColumnToggleItem(items, "Bitrate", () => _viewModel.IsBitrateColumnVisible, v => _viewModel.IsBitrateColumnVisible = v);
        AddColumnToggleItem(items, "Sample Rate", () => _viewModel.IsSampleRateColumnVisible, v => _viewModel.IsSampleRateColumnVisible = v);
        AddColumnToggleItem(items, "Channels", () => _viewModel.IsChannelsColumnVisible, v => _viewModel.IsChannelsColumnVisible = v);
        AddColumnToggleItem(items, "File Size", () => _viewModel.IsFileSizeColumnVisible, v => _viewModel.IsFileSizeColumnVisible = v);
        AddColumnToggleItem(items, "Tag Formats", () => _viewModel.IsTagFormatsColumnVisible, v => _viewModel.IsTagFormatsColumnVisible = v);
        AddColumnToggleItem(items, "Modified", () => _viewModel.IsModifiedColumnVisible, v => _viewModel.IsModifiedColumnVisible = v);

        menu.ItemsSource = items;
        menu.ShowAt(FileGrid);
        e.Handled = true;
    }

    private static void AddColumnToggleItem(List<Control> items, string header, Func<bool> getCurrent, Action<bool> setNew)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = getCurrent(),
        };
        item.Click += (_, _) => setNew(item.IsChecked);
        items.Add(item);
    }
}
