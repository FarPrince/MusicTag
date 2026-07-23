namespace MusicTag.Core.Settings;

/// <summary>
/// Persisted app-level settings, exactly per plan section 6. Serialized as-is by
/// <see cref="ISettingsService"/> via <c>System.Text.Json</c> — plain mutable properties (not
/// a record) since callers (e.g. the App-side SettingsViewModel) load a snapshot, mutate a few
/// fields, and save the same instance back rather than needing structural-equality/with-style
/// semantics the way <c>TagFieldSet</c> does.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Folder opened automatically on startup when no Explorer-launch arg is present
    /// (see plan section 7's startup arg-parsing precedence) — null/absent means "start empty
    /// with the Open Folder prompt."</summary>
    public string? DefaultStartupFolder { get; set; }

    /// <summary>"System" | "Light" | "Dark". Only persisted here in M7 — actually switching
    /// the app's live theme based on this value is M8 scope (plan section 8).</summary>
    public string Theme { get; set; } = "System";

    /// <summary>"Acrylic" | "Mica" — per user feedback, the toolbar's quick toggle button
    /// (formerly a Light/Dark/System theme cycle — that's still reachable via
    /// SettingsWindow's own theme picker) now cycles the main window's backdrop material
    /// instead. Acrylic is the default since it was the prior hardcoded value across the app
    /// (chosen earlier for its visibly see-through look vs. Mica's near-opaque one).</summary>
    public string Backdrop { get; set; } = "Acrylic";

    /// <summary>Captured on <c>MainWindow.Closing</c>, restored (clamped to the current virtual
    /// screen bounds) on startup — null on first-ever run.</summary>
    public WindowPlacement? LastWindowPlacement { get; set; }

    /// <summary>Mirrors the last known Explorer-integration registration state. Note this is a
    /// convenience cache, not the source of truth — <see cref="Integration.IExplorerIntegrationService.IsRegistered"/>
    /// (backed by the real registry) is authoritative; the App-side SettingsViewModel re-derives
    /// the live value from that on load instead of trusting this field blindly.</summary>
    public bool ExplorerIntegrationRegistered { get; set; }

    /// <summary>Which of <see cref="Models.SeparatorNormalization.FieldNames"/> the toolbar's
    /// "Normalize Separators" button applies to, per user request ("Create a setting that
    /// allows me select which fields should be changed"). Defaults to the classic multi-value
    /// fields — Title/Album/Comment start unchecked since they're typically single-value, but
    /// SettingsWindow lets the user enable any of the 7.</summary>
    public HashSet<string> SeparatorNormalizationFields { get; set; } = ["Artist", "AlbumArtist", "Composer", "Genre"];

    /// <summary>Directories the toolbar's "Search Lyrics" button
    /// (<see cref="MusicTag.Core.Services.ILyricsSearchService"/>) scans recursively for songs missing a
    /// sidecar .lrc file, per user request. Empty by default — the user must configure at least
    /// one directory via Settings before the button has anything to search.</summary>
    public List<string> LyricsSearchDirectories { get; set; } = [];

    /// <summary>Per-column visibility, width, and display order for the main window's file grid,
    /// keyed by the DataGridColumn's x:Name (see MainWindow.xaml/.xaml.cs). Captured on Closing,
    /// restored on startup, per user request that "the tags I selected in the headers and the
    /// width of each tag" — and, per a follow-up request, the order the user placed them in —
    /// persist across sessions. Empty on first-ever run, leaving the XAML-declared defaults in
    /// effect.</summary>
    public Dictionary<string, GridColumnState> GridColumns { get; set; } = new();
}

/// <summary>One grid column's persisted state — see <see cref="AppSettings.GridColumns"/>.
/// Width and WidthUnitType together are always the column's own <c>DataGridColumn.Width</c>
/// (a WPF <c>DataGridLength</c>'s Value + UnitType) captured verbatim, NOT a synthesized pixel
/// value derived from <c>ActualWidth</c> — an earlier version of this feature captured
/// ActualWidth unconditionally, which silently converted every Star-sized column (Filename/
/// Title/Artist/Album/etc. — "fill remaining space, scale with the window") into a fixed Pixel
/// width on the very next restore even for a column the user never touched, breaking window-resize
/// scaling and causing columns to get clipped by the side panel — reported back after v1.6
/// shipped. WidthUnitType is a plain string (a WPF <c>DataGridLengthUnitType</c> enum name —
/// "Auto"/"Pixel"/"Star"/etc. — rather than the enum type itself, since MusicTag.Core has no WPF
/// reference) and defaults to null so a settings file saved before this fix (or any other file
/// missing it) is recognized as unreliable pixel-only data — MainWindow.xaml.cs's
/// RestoreGridColumnState skips restoring Width entirely for a column whose saved state has no
/// WidthUnitType, leaving whatever XAML declared (Star for the wide text columns, Auto for the
/// narrow numeric ones) in effect, rather than trusting a value that might have been an
/// accidentally-frozen Star column. DisplayIndex defaults to -1 ("no saved order") for the same
/// reason — a settings file written before column-order persistence existed shouldn't be misread
/// as "every column wants index 0"; RestoreGridColumnState only reorders columns at all once
/// every column in the grid has a real (non-negative) saved index.</summary>
public sealed record GridColumnState(bool Visible, double Width, int DisplayIndex = -1, string? WidthUnitType = null);

/// <summary>
/// A restorable window position/size/maximized-state snapshot. Not given an exact shape by the
/// plan text itself (section 6 only declares the <c>WindowPlacement?</c> property on
/// <see cref="AppSettings"/>) — this is the natural minimal shape for "capture/restore window
/// placement, clamped to current virtual screen bounds": normal-state bounds (so a maximized
/// window still has something sensible to restore to) plus whether it was maximized.
/// </summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool IsMaximized);
