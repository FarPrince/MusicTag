using Avalonia.Controls;

namespace MusicTag.App.Services;

/// <summary>
/// Thin Avalonia-facing wrapper around theme/backdrop switching, matching the same seam pattern
/// as <see cref="IDialogService"/>/<see cref="IFilePickerService"/> so
/// <see cref="ViewModels.SettingsViewModel"/> can trigger a live theme switch without referencing
/// Avalonia types directly.
/// </summary>
public interface IThemeService
{
    /// <summary>Must be called once, before the first <see cref="ApplyBackdrop"/> call, with the
    /// app's single long-lived main window — needed so backdrop changes have a window to apply
    /// their <c>TransparencyLevelHint</c> to. Unlike the WPF-UI original, Avalonia's own
    /// <c>RequestedThemeVariant</c> already tracks OS theme-change notifications live for
    /// "System" mode with no separate per-window watcher needed, so this registration exists
    /// only for backdrop, not theme.</summary>
    void RegisterMainWindow(Window mainWindow);

    /// <summary>Applies "System" | "Light" | "Dark" (the exact <see cref="Core.Settings.AppSettings.Theme"/>
    /// values) immediately and app-wide via <see cref="Avalonia.Application.RequestedThemeVariant"/>.
    /// An unrecognized value falls back to "System" rather than throwing, since a hand-edited
    /// settings.json shouldn't be able to crash startup.</summary>
    void ApplyTheme(string theme);

    /// <summary>Applies "Acrylic" | "Mica" (the exact <see cref="Core.Settings.AppSettings.Backdrop"/>
    /// values) to the main window's backdrop material via <c>Window.TransparencyLevelHint</c>.
    /// Both degrade gracefully with a documented, honest fallback chain on platforms/compositors
    /// that don't support the requested transparency level (common on Linux — see
    /// <see cref="ThemeService"/>'s own doc comment) rather than silently doing nothing. An
    /// unrecognized value falls back to Acrylic for the same "never let a hand-edited
    /// settings.json crash" reason as <see cref="ApplyTheme"/>.</summary>
    void ApplyBackdrop(string backdrop);
}
