using System.Diagnostics;

namespace MusicTag.Core.Integration;

/// <summary>
/// Linux counterpart of <see cref="ExplorerIntegrationService"/> — same
/// <see cref="IExplorerIntegrationService"/> contract (Register/Unregister/IsRegistered), but
/// there is no single OS-wide "shell right-click" registry on Linux the way there is on Windows,
/// so this reaches the same "Open with MusicTag on a folder" goal through two independent,
/// desktop-standard mechanisms instead of one:
/// <list type="bullet">
/// <item>a per-user <c>.desktop</c> launcher entry declaring
/// <c>MimeType=inode/directory;</c>, which every freedesktop.org-compliant file manager (GNOME
/// Files/Nautilus, Dolphin, etc. — Fedora Workstation ships Nautilus) offers in its folder
/// "Open With" list;</item>
/// <item>a Nautilus script (<c>~/.local/share/nautilus/scripts</c>), which additionally puts a
/// one-click "Open with MusicTag" entry directly in Nautilus's folder-background right-click
/// menu under "Scripts" — the closest Linux/GNOME analogue of the Windows
/// <c>Directory\Background\shell</c> entry <see cref="ExplorerIntegrationService"/> writes.
/// Harmless (just not offered) on a file manager other than Nautilus.</item>
/// </list>
/// Both live entirely under the user's home directory (<c>~/.local/share/...</c>) — no elevation
/// needed, matching <see cref="ExplorerIntegrationService"/>'s HKCU-only, no-admin design. All
/// real filesystem access is behind <see cref="ILinuxDesktopFileWriter"/> (see
/// <c>LinuxFileManagerIntegrationServiceTests</c>, which asserts these exact paths/contents
/// against a fake implementation without touching the real home directory).
/// </summary>
public sealed class LinuxFileManagerIntegrationService : IExplorerIntegrationService
{
    private const string DesktopEntryRelativePath = ".local/share/applications/musictag.desktop";
    private const string NautilusScriptRelativePath = ".local/share/nautilus/scripts/Open with MusicTag";

    private readonly ILinuxDesktopFileWriter _writer;

    public LinuxFileManagerIntegrationService(ILinuxDesktopFileWriter writer)
    {
        _writer = writer;
    }

    public bool IsRegistered() => _writer.FileExists(DesktopEntryPath());

    public void Register()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the current process's executable path.");

        var iconValue = ResolveIconValue(exePath);

        _writer.WriteFile(DesktopEntryPath(), BuildDesktopEntry(exePath, iconValue), executable: true);
        _writer.WriteFile(NautilusScriptPath(), BuildNautilusScript(exePath), executable: true);

        // Best-effort: refreshes the desktop database's MIME-association cache so the new
        // "Open With" entry shows up immediately instead of only after the next login/file
        // manager restart. Not every distro/session has this tool (or even a D-Bus session to
        // notify) — a failure here must never make Register() itself fail, since both files
        // above are already written and functionally registered regardless.
        TryRun("update-desktop-database", $"\"{Path.GetDirectoryName(DesktopEntryPath())}\"");
    }

    public void Unregister()
    {
        _writer.DeleteFile(DesktopEntryPath());
        _writer.DeleteFile(NautilusScriptPath());
        TryRun("update-desktop-database", $"\"{Path.GetDirectoryName(DesktopEntryPath())}\"");
    }

    private string DesktopEntryPath() => Path.Combine(_writer.GetHomeDirectory(), DesktopEntryRelativePath);

    private string NautilusScriptPath() => Path.Combine(_writer.GetHomeDirectory(), NautilusScriptRelativePath);

    /// <summary>Prefers a <c>logo.png</c> sitting next to the running executable (present when
    /// installed from the Fedora RPM or a self-contained publish folder — mirrors
    /// <see cref="ExplorerIntegrationService"/> pointing at the .exe's own embedded icon), falling
    /// back to the icon-theme name the RPM also installs under
    /// <c>/usr/share/icons/hicolor/*/apps/musictag.png</c> so a themed icon still resolves even
    /// if the sibling file isn't found (e.g. a relocated/symlinked binary).</summary>
    private string ResolveIconValue(string exePath)
    {
        var exeDirectory = Path.GetDirectoryName(exePath);
        if (exeDirectory is not null)
        {
            var siblingIconPath = Path.Combine(exeDirectory, "logo.png");
            if (_writer.FileExists(siblingIconPath))
            {
                return siblingIconPath;
            }
        }

        return "musictag";
    }

    private static string BuildDesktopEntry(string exePath, string iconValue) =>
        $"""
         [Desktop Entry]
         Type=Application
         Name=MusicTag
         Comment=Edit audio file tags
         Exec="{exePath}" %f
         Icon={iconValue}
         Terminal=false
         Categories=AudioVideo;Audio;
         MimeType=inode/directory;
         NoDisplay=false
         StartupWMClass=MusicTag

         """;

    /// <summary>Nautilus exposes every executable file under
    /// <c>~/.local/share/nautilus/scripts</c> in its right-click "Scripts" submenu, run with the
    /// clicked location passed via environment variables rather than argv — set for a selected
    /// item (right-click on the folder itself) or, with nothing selected (right-click on empty
    /// folder background — the direct analogue of the Windows
    /// <c>Directory\Background\shell</c> entry), the folder currently being viewed.</summary>
    private static string BuildNautilusScript(string exePath) =>
        $"""
         #!/bin/sh
         if [ -n "$NAUTILUS_SCRIPT_SELECTED_FILE_PATHS" ]; then
             target=$(printf '%s\n' "$NAUTILUS_SCRIPT_SELECTED_FILE_PATHS" | head -n1)
         else
             target=$(printf '%s' "$NAUTILUS_SCRIPT_CURRENT_URI" | sed 's#^file://##')
         fi
         exec "{exePath}" "$target"

         """;

    private static void TryRun(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            process?.WaitForExit(2000);
        }
        catch
        {
            // Best-effort only — see Register()'s doc comment.
        }
    }
}
