namespace MusicTag.Core.Integration;

/// <summary>
/// Thin seam over the small slice of <c>Microsoft.Win32.Registry</c> that
/// <see cref="ExplorerIntegrationService"/> needs, all scoped to HKCU-relative subkey paths
/// (e.g. <c>"Software\Classes\Directory\shell\MusicTag"</c>) — exactly the plan section 7 goal
/// of "wrap real Microsoft.Win32.Registry access behind IRegistryKeyWrapper so it's testable
/// without touching the real registry." <see cref="ExplorerIntegrationServiceTests"/> asserts
/// exact key/value strings against a fake implementation of this interface; the real
/// implementation is a one-line-per-member pass-through to <c>Registry.CurrentUser</c>.
/// </summary>
public interface IRegistryKeyWrapper
{
    /// <summary>Creates (or opens, if it already exists) the given HKCU-relative subkey path
    /// and sets its default (unnamed) value — the registry mechanism a context-menu entry's
    /// display text and command line are both stored as.</summary>
    void SetDefaultValue(string subKeyPath, string value);

    /// <summary>Creates (or opens) the given HKCU-relative subkey path and sets a named
    /// (non-default) value on it — used for a context-menu entry's "Icon" value, which
    /// Explorer reads separately from the entry's default (display-text) value.</summary>
    void SetNamedValue(string subKeyPath, string valueName, string value);

    /// <summary>Returns the default (unnamed) value of the given HKCU-relative subkey path, or
    /// null if the key doesn't exist. Used by <see cref="IExplorerIntegrationService.IsRegistered"/>.</summary>
    string? GetDefaultValue(string subKeyPath);

    /// <summary>Deletes the given HKCU-relative subkey path and everything under it. A no-op
    /// (never throws) if the key doesn't exist — mirrors
    /// <c>Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false)</c>,
    /// which the plan calls for explicitly so Unregister is safe to call even when
    /// registration was never actually performed (or was only partially performed).</summary>
    void DeleteTree(string subKeyPath);
}
