using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MusicTag.Core.Integration;

/// <summary>
/// Real <see cref="IRegistryKeyWrapper"/> backed by <c>Microsoft.Win32.Registry.CurrentUser</c>.
/// Every subkey path passed in here is HKCU-relative (e.g.
/// <c>"Software\Classes\Directory\shell\MusicTag"</c>) — this class never touches any other
/// registry hive, matching plan section 7's "all under HKCU (no elevation needed)."
///
/// <see cref="MusicTag.Core"/> itself targets plain <c>net8.0</c> (not <c>net8.0-windows</c> —
/// see Directory.Build.props) so it stays portable/testable, but this one class is inherently
/// Windows-only; <see cref="SupportedOSPlatformAttribute"/> documents that precisely (suppressing
/// the CA1416 platform-compatibility warning for this file) rather than suppressing it
/// solution-wide. MusicTag.App (which does target net8.0-windows) is the only real caller, via
/// DI registration in App.xaml.cs.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryKeyWrapper : IRegistryKeyWrapper
{
    public void SetDefaultValue(string subKeyPath, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKeyPath, writable: true)
            ?? throw new InvalidOperationException($"Could not create or open registry key \"{subKeyPath}\".");

        key.SetValue(null, value);
    }

    public string? GetDefaultValue(string subKeyPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(subKeyPath, writable: false);
        return key?.GetValue(null) as string;
    }

    public void DeleteTree(string subKeyPath)
        => Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
}
