namespace MusicTag.Core.Integration;

/// <summary>
/// Registers/unregisters an "Open with MusicTag" right-click entry for a folder — exposed as
/// the Register/Unregister toggle in Settings. Two implementations, chosen at DI registration
/// time by the running OS: <see cref="ExplorerIntegrationService"/> (Windows, HKCU registry) and
/// <see cref="LinuxFileManagerIntegrationService"/> (Linux, a per-user <c>.desktop</c> entry plus
/// a Nautilus script). No elevation is required for either — both live entirely under the
/// current user's own profile.
/// </summary>
public interface IExplorerIntegrationService
{
    /// <summary>True if the folder-shell registry key is currently present. Used to drive the
    /// Settings toggle's initial state and label (Register vs. Unregister) — this is the live,
    /// authoritative check, not a cached flag.</summary>
    bool IsRegistered();

    /// <summary>Creates all four registry values described in plan section 7 (shell + shell
    /// Background roots, each with a display-text key and a command subkey), using
    /// <see cref="Environment.ProcessPath"/> (double-quoted, since paths can contain spaces) as
    /// the command target.</summary>
    void Register();

    /// <summary>Removes both registry key trees. Safe to call even if registration was never
    /// performed or only partially succeeded — never throws for a missing key.</summary>
    void Unregister();
}
