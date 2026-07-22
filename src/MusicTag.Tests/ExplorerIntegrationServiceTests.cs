using MusicTag.Core.Integration;

namespace MusicTag.Tests;

/// <summary>
/// Asserts the exact HKCU key/value strings from plan section 7 against a fake
/// <see cref="IRegistryKeyWrapper"/> — never touches the real registry, per the plan's explicit
/// testability requirement ("Wrap real Microsoft.Win32.Registry access behind
/// IRegistryKeyWrapper so it's testable without touching the real registry").
/// </summary>
public class ExplorerIntegrationServiceTests
{
    private const string ShellKeyPath = @"Software\Classes\Directory\shell\MusicTag";
    private const string ShellCommandKeyPath = @"Software\Classes\Directory\shell\MusicTag\command";
    private const string BackgroundShellKeyPath = @"Software\Classes\Directory\Background\shell\MusicTag";
    private const string BackgroundShellCommandKeyPath = @"Software\Classes\Directory\Background\shell\MusicTag\command";

    [Fact]
    public void Register_SetsExactFourKeyValuePairs()
    {
        var registry = new FakeRegistryKeyWrapper();
        var service = new ExplorerIntegrationService(registry);
        var expectedExePath = Environment.ProcessPath!;

        service.Register();

        Assert.Equal("Open with MusicTag", registry.GetDefaultValue(ShellKeyPath));
        Assert.Equal($"\"{expectedExePath}\" \"%1\"", registry.GetDefaultValue(ShellCommandKeyPath));

        Assert.Equal("Open with MusicTag", registry.GetDefaultValue(BackgroundShellKeyPath));
        Assert.Equal($"\"{expectedExePath}\" \"%V\"", registry.GetDefaultValue(BackgroundShellCommandKeyPath));

        // Exactly these four values — nothing extra written anywhere else.
        Assert.Equal(4, registry.Values.Count);
    }

    [Fact]
    public void IsRegistered_FalseBeforeRegister_TrueAfter()
    {
        var registry = new FakeRegistryKeyWrapper();
        var service = new ExplorerIntegrationService(registry);

        Assert.False(service.IsRegistered());

        service.Register();

        Assert.True(service.IsRegistered());
    }

    [Fact]
    public void Unregister_RemovesBothKeyTrees()
    {
        var registry = new FakeRegistryKeyWrapper();
        var service = new ExplorerIntegrationService(registry);
        service.Register();

        service.Unregister();

        Assert.False(service.IsRegistered());
        Assert.Null(registry.GetDefaultValue(ShellKeyPath));
        Assert.Null(registry.GetDefaultValue(ShellCommandKeyPath));
        Assert.Null(registry.GetDefaultValue(BackgroundShellKeyPath));
        Assert.Null(registry.GetDefaultValue(BackgroundShellCommandKeyPath));
        Assert.Empty(registry.Values);
    }

    [Fact]
    public void Unregister_WithoutPriorRegister_DoesNotThrow()
    {
        var registry = new FakeRegistryKeyWrapper();
        var service = new ExplorerIntegrationService(registry);

        var exception = Record.Exception(() => service.Unregister());

        Assert.Null(exception);
    }

    [Fact]
    public void Register_IsIdempotent_RunningTwiceLeavesTheSameFourValues()
    {
        var registry = new FakeRegistryKeyWrapper();
        var service = new ExplorerIntegrationService(registry);

        service.Register();
        service.Register();

        Assert.Equal(4, registry.Values.Count);
        Assert.True(service.IsRegistered());
    }

    /// <summary>
    /// In-memory stand-in for the real registry, keyed by HKCU-relative subkey path -> default
    /// value. <see cref="DeleteTree"/> mirrors <c>DeleteSubKeyTree(throwOnMissingSubKey: false)</c>
    /// semantics: removes the exact path plus anything nested under it, and never throws when
    /// nothing was there to remove.
    /// </summary>
    private sealed class FakeRegistryKeyWrapper : IRegistryKeyWrapper
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, string> Values => _values;

        public void SetDefaultValue(string subKeyPath, string value) => _values[subKeyPath] = value;

        public string? GetDefaultValue(string subKeyPath) => _values.GetValueOrDefault(subKeyPath);

        public void DeleteTree(string subKeyPath)
        {
            var prefix = subKeyPath + @"\";
            var keysToRemove = _values.Keys
                .Where(k => string.Equals(k, subKeyPath, StringComparison.OrdinalIgnoreCase)
                            || k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _values.Remove(key);
            }
        }
    }
}
