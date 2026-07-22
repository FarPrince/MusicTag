namespace MusicTag.Core.Settings;

/// <summary>
/// Persists/loads <see cref="AppSettings"/> to a JSON file, per plan section 6. Both members are
/// deliberately synchronous — the file is tiny and touched only at startup, on Settings-window
/// Save, and on MainWindow.Closing, none of which are hot paths worth an async ceremony for.
/// </summary>
public interface ISettingsService
{
    /// <summary>Loads settings from disk. Must never throw: a missing file returns
    /// <c>new AppSettings()</c> (first-ever run), and a present-but-corrupt/unreadable file is
    /// renamed to a <c>.bak</c> sibling (best-effort — a failure to even do that is swallowed
    /// too) before likewise returning defaults, per plan section 6's "must never crash on a
    /// hand-edited/corrupt file."</summary>
    AppSettings Load();

    /// <summary>Writes settings atomically (temp file + <see cref="File.Replace(string,string,string?)"/>,
    /// or a plain move on first-ever save when no destination file exists yet to replace) so a
    /// crash/power-loss mid-write can never leave a half-written settings.json behind.</summary>
    void Save(AppSettings settings);
}
