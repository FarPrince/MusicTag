using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace MusicTag.App.Services;

/// <summary>
/// M8: real System/Light/Dark switching via <see cref="ApplicationThemeManager"/> (app-wide
/// resource-dictionary swap) and <see cref="SystemThemeWatcher"/> ("System" mode's live
/// follow-the-OS behavior), per plan sections 5 and 8.
///
/// "System" mode both applies the OS's current theme immediately
/// (<see cref="ApplicationThemeManager.ApplySystemTheme(bool)"/>) and watches the registered main
/// window for subsequent OS theme-change notifications
/// (<see cref="SystemThemeWatcher.Watch"/>) so the app keeps following Windows' setting live
/// while running — switching away from "System" to an explicit Light/Dark first calls
/// <see cref="SystemThemeWatcher.UnWatch"/> so a later OS-level theme change can't silently
/// override the user's explicit choice.
///
/// <b>Confirmed by an actual crash, not assumed:</b> both <c>SystemThemeWatcher.Watch/UnWatch</c>
/// and <c>WindowBackdrop.ApplyBackdrop</c> require the target window to already be loaded (have
/// a real HWND) — calling either before the very first <c>Show()</c>/<c>Loaded</c> throws
/// (<c>UnWatch</c> literally: "You cannot unwatch a window that is not yet loaded."). Since
/// <c>App.OnStartup</c> deliberately applies the persisted theme *before* showing the main
/// window (so it never flashes the wrong theme), this class defers every window-touching call
/// (<see cref="ApplyWindowState"/>) until <see cref="Window.Loaded"/> actually fires, replaying
/// whatever theme was requested in the meantime. The <see cref="ApplicationThemeManager"/> calls
/// themselves are safe pre-load (they only touch the shared <c>Application.Resources</c>
/// dictionary) and always run immediately, so the correct colors are already in place by the
/// time the window's own <c>WindowBackdropType="Acrylic"</c> XAML attribute applies its backdrop
/// on load — no flash either way. Acrylic is the default (rather than Mica) per user feedback:
/// Mica is deliberately subtle/near-opaque by Microsoft's own design intent, not the visibly
/// see-through blurred-glass look originally wanted — now user-toggleable at runtime via
/// <see cref="ApplyBackdrop"/> (the toolbar's quick toggle button).
///
/// The main window's Acrylic backdrop is re-asserted explicitly on every apply once loaded —
/// this costs nothing to call redundantly, and guarantees the already-open main window's
/// backdrop repaints correctly for the new theme without relying on undocumented internal
/// behavior of <see cref="ApplicationThemeManager.Apply"/> (which is only confirmed to update
/// the shared theme resource dictionary, not necessarily to walk every open <see cref="Window"/>
/// and refresh its backdrop composition). Secondary windows (SettingsWindow,
/// ShortcutsReferenceWindow) need no such treatment — they're built fresh per open (see
/// DialogService) and pick up whatever theme is current at construction time via their own
/// declared <c>WindowBackdropType="Acrylic"</c> XAML attribute.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private Window? _mainWindow;
    private string _currentTheme = "System";
    private string _currentBackdrop = "Acrylic";

    public void RegisterMainWindow(Window mainWindow)
    {
        _mainWindow = mainWindow;
        mainWindow.Loaded += OnMainWindowLoaded;
    }

    private void OnMainWindowLoaded(object? sender, RoutedEventArgs e) => ApplyWindowState();

    public void ApplyTheme(string theme)
    {
        _currentTheme = theme;
        var backdrop = ParseBackdrop(_currentBackdrop);

        switch (theme)
        {
            case "Light":
                ApplicationThemeManager.Apply(ApplicationTheme.Light, backdrop, updateAccent: true);
                break;

            case "Dark":
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, backdrop, updateAccent: true);
                break;

            case "System":
            default:
                // Falls back here for "System" and for any unrecognized value (e.g. a
                // hand-edited settings.json) rather than throwing.
                ApplicationThemeManager.ApplySystemTheme(updateAccent: true);
                break;
        }

        ApplyWindowState();
    }

    /// <summary>Per user feedback ("Theme button should switch between Acrylic and Mica") —
    /// the toolbar's quick toggle button used to cycle Light/Dark/System (still reachable via
    /// SettingsWindow) and now cycles the backdrop material instead. Independent of
    /// <see cref="ApplyTheme"/>: switching backdrop must not touch the current Light/Dark/System
    /// choice, and vice versa — <see cref="ApplyWindowState"/> re-asserts both every time either
    /// one changes, since <c>WindowBackdrop.ApplyBackdrop</c> needs the current backdrop value
    /// regardless of which call triggered it.</summary>
    public void ApplyBackdrop(string backdrop)
    {
        _currentBackdrop = backdrop;
        ApplyWindowState();
    }

    private static WindowBackdropType ParseBackdrop(string backdrop) => backdrop switch
    {
        "Mica" => WindowBackdropType.Mica,
        _ => WindowBackdropType.Acrylic,
    };

    /// <summary>The window-touching half of <see cref="ApplyTheme"/>/<see cref="ApplyBackdrop"/> —
    /// split out so it can be (a) skipped entirely before the main window has loaded, and (b)
    /// replayed once it does (see <see cref="OnMainWindowLoaded"/>), per this class's own doc
    /// comment on why that split exists.</summary>
    private void ApplyWindowState()
    {
        if (_mainWindow is not { IsLoaded: true } window)
            return;

        var backdrop = ParseBackdrop(_currentBackdrop);

        // Always start from a clean slate — an explicit Light/Dark pick must not keep
        // following a subsequent OS theme change left over from a prior "System" selection.
        SystemThemeWatcher.UnWatch(window);

        // "System" plus any unrecognized value (see ApplyTheme's own fallback) watches for
        // live OS theme changes; only an explicit "Light"/"Dark" opts out.
        if (_currentTheme != "Light" && _currentTheme != "Dark")
        {
            SystemThemeWatcher.Watch(window, backdrop, true);
        }

        WindowBackdrop.ApplyBackdrop(window, backdrop);
    }
}
