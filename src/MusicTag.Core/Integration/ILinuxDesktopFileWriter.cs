namespace MusicTag.Core.Integration;

/// <summary>
/// Thin seam over the small slice of filesystem access that
/// <see cref="LinuxFileManagerIntegrationService"/> needs — the Linux analogue of
/// <see cref="IRegistryKeyWrapper"/> (same "wrap the real OS-integration mechanism behind an
/// interface so it's testable without touching the real filesystem/registry" pattern).
/// <see cref="LinuxFileManagerIntegrationServiceTests"/> asserts exact paths/content against a
/// fake implementation of this interface; the real implementation is a thin pass-through to
/// <c>System.IO</c> rooted at the user's home directory.
/// </summary>
public interface ILinuxDesktopFileWriter
{
    /// <summary>The current user's home directory (e.g. <c>/home/alice</c>) — every path this
    /// service writes to is relative to this.</summary>
    string GetHomeDirectory();

    /// <summary>True if a regular file currently exists at <paramref name="path"/>.</summary>
    bool FileExists(string path);

    /// <summary>Creates any missing parent directories and writes <paramref name="content"/> to
    /// <paramref name="path"/>, overwriting whatever was there before. When
    /// <paramref name="executable"/> is true, also sets the owner-executable permission bit —
    /// required for a Nautilus script to be offered at all (Nautilus silently ignores
    /// non-executable files in its scripts folder).</summary>
    void WriteFile(string path, string content, bool executable);

    /// <summary>Deletes the file at <paramref name="path"/>. A no-op (never throws) if it
    /// doesn't exist — mirrors <see cref="IRegistryKeyWrapper.DeleteTree"/>'s tolerance for
    /// "unregister when never registered."</summary>
    void DeleteFile(string path);
}
