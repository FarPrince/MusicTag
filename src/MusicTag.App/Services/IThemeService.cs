namespace MusicTag.App.Services;

/// <summary>
/// M8: thin WPF-facing wrapper around WPF-UI's static <c>Wpf.Ui.Appearance.ApplicationThemeManager</c>/
/// <c>SystemThemeWatcher</c> APIs, matching the same seam pattern as <see cref="IDialogService"/>/
/// <see cref="IFilePickerService"/> (plan section 2: "thin WPF-facing wrappers so VMs stay
/// unit-testable") — not itself named by the plan, but a natural, minimal extension of that
/// established pattern so <see cref="ViewModels.SettingsViewModel"/> can trigger a live theme
/// switch without referencing WPF-UI's appearance types directly.
/// </summary>
public interface IThemeService
{
    /// <summary>Must be called once, before the first <see cref="ApplyTheme"/> call, with the
    /// app's single long-lived main window — needed so "System" mode can watch it for live OS
    /// theme-change notifications (<c>SystemThemeWatcher.Watch</c>) and so an explicit Light/Dark
    /// selection can defensively re-assert that window's Mica backdrop on a live switch (see
    /// <see cref="ThemeService"/>'s own doc comment for why that's done explicitly rather than
    /// assumed). Secondary windows (SettingsWindow, ShortcutsReferenceWindow) need no such
    /// registration — they're built fresh after any theme change and already render under
    /// whatever theme is current when they're constructed.</summary>
    void RegisterMainWindow(System.Windows.Window mainWindow);

    /// <summary>Applies "System" | "Light" | "Dark" (the exact <see cref="Core.Settings.AppSettings.Theme"/>
    /// values) immediately and app-wide — used both once at startup (from the persisted setting)
    /// and instantly whenever <see cref="ViewModels.SettingsViewModel.Theme"/> changes, per plan
    /// section 8. An unrecognized value falls back to "System" rather than throwing, since a
    /// hand-edited settings.json shouldn't be able to crash startup.</summary>
    void ApplyTheme(string theme);

    /// <summary>Applies "Acrylic" | "Mica" (the exact <see cref="Core.Settings.AppSettings.Backdrop"/>
    /// values) to the main window's backdrop material — per user feedback, the toolbar's quick
    /// toggle button now cycles this instead of Light/Dark/System. An unrecognized value falls
    /// back to Acrylic for the same "never let a hand-edited settings.json crash" reason as
    /// <see cref="ApplyTheme"/>.</summary>
    void ApplyBackdrop(string backdrop);
}
