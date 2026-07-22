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
}

/// <summary>
/// A restorable window position/size/maximized-state snapshot. Not given an exact shape by the
/// plan text itself (section 6 only declares the <c>WindowPlacement?</c> property on
/// <see cref="AppSettings"/>) — this is the natural minimal shape for "capture/restore window
/// placement, clamped to current virtual screen bounds": normal-state bounds (so a maximized
/// window still has something sensible to restore to) plus whether it was maximized.
/// </summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool IsMaximized);
