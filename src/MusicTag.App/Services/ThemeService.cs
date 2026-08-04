using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace MusicTag.App.Services;

/// <summary>
/// System/Light/Dark switching via <see cref="Application.RequestedThemeVariant"/> and backdrop
/// (Acrylic/Mica) via <see cref="Window.TransparencyLevelHint"/> — Avalonia's own equivalents of
/// WPF-UI's <c>ApplicationThemeManager</c>/<c>WindowBackdrop</c>, and considerably simpler:
/// Avalonia's Fluent theme already follows live OS theme-change notifications on its own
/// whenever <see cref="Application.RequestedThemeVariant"/> is <see cref="ThemeVariant.Default"/>
/// ("System") — there is no separate watcher to explicitly start/stop the way WPF-UI's
/// <c>SystemThemeWatcher.Watch/UnWatch</c> required, and no "window must already be loaded before
/// you can touch it" restriction either (contrast the WPF original's documented crash risk),
/// so <see cref="ApplyTheme"/> and <see cref="ApplyBackdrop"/> can both run at any time —
/// including, per App.axaml.cs, before the main window's first <c>Show()</c>.
///
/// <see cref="Window.TransparencyLevelHint"/> takes an ordered fallback list, not a single
/// value — Avalonia tries each entry in turn and renders whichever the current platform/
/// compositor actually supports. Acrylic/Mica-equivalent blur is a genuinely inconsistent
/// feature across Linux compositors (works under a compositing X11/Wayland session with a
/// GNOME/KDE-family window manager; silently has nothing to fall back to under a
/// non-compositing setup) — <see cref="WindowTransparencyLevel.None"/> is always the last
/// fallback so an unsupported environment still renders a solid, fully legible window rather
/// than a broken/transparent one.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private Window? _mainWindow;
    private string _currentBackdrop = "Acrylic";

    public void RegisterMainWindow(Window mainWindow)
    {
        _mainWindow = mainWindow;
        ApplyBackdropToWindow();
    }

    public void ApplyTheme(string theme)
    {
        if (Application.Current is not { } app)
            return;

        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            // Falls back here for "System" and for any unrecognized value (e.g. a hand-edited
            // settings.json) rather than throwing.
            _ => ThemeVariant.Default,
        };
    }

    public void ApplyBackdrop(string backdrop)
    {
        _currentBackdrop = backdrop;
        ApplyBackdropToWindow();
    }

    private void ApplyBackdropToWindow()
    {
        if (_mainWindow is not { } window)
            return;

        window.TransparencyLevelHint = _currentBackdrop switch
        {
            "Mica" =>
            [
                WindowTransparencyLevel.Mica,
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Blur,
                WindowTransparencyLevel.None,
            ],
            _ =>
            [
                WindowTransparencyLevel.AcrylicBlur,
                WindowTransparencyLevel.Blur,
                WindowTransparencyLevel.None,
            ],
        };
    }
}
