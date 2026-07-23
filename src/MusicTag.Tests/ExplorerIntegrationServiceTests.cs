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
    public void Register_SetsExactSixKeyValuePairs()
    {
        var registry = new FakeRegistryKeyWrapper();
        var service = new ExplorerIntegrationService(registry);
        var expectedExePath = Environment.ProcessPath!;

        service.Register();

        Assert.Equal("Open with MusicTag", registry.GetDefaultValue(ShellKeyPath));
        Assert.Equal($"{expectedExePath},0", registry.NamedValues[(ShellKeyPath, "Icon")]);
        Assert.Equal($"\"{expectedExePath}\" \"%1\"", registry.GetDefaultValue(ShellCommandKeyPath));

        Assert.Equal("Open with MusicTag", registry.GetDefaultValue(BackgroundShellKeyPath));
        Assert.Equal($"{expectedExePath},0", registry.NamedValues[(BackgroundShellKeyPath, "Icon")]);
        Assert.Equal($"\"{expectedExePath}\" \"%V\"", registry.GetDefaultValue(BackgroundShellCommandKeyPath));

        // Exactly these six values — nothing extra written anywhere else.
        Assert.Equal(6, registry.TotalValueCount);
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
        Assert.Equal(0, registry.TotalValueCount);
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
    public void Register_IsIdempotent_RunningTwiceLeavesTheSameSixValues()
    {
        var registry = new FakeRegistryKeyWrapper();
        var service = new ExplorerIntegrationService(registry);

        service.Register();
        service.Register();

        Assert.Equal(6, registry.TotalValueCount);
        Assert.True(service.IsRegistered());
    }

    /// <summary>
    /// In-memory stand-in for the real registry, keyed by HKCU-relative subkey path -> default
    /// value, plus a separate table for named (non-default) values like "Icon".
    /// <see cref="DeleteTree"/> mirrors <c>DeleteSubKeyTree(throwOnMissingSubKey: false)</c>
    /// semantics: removes the exact path plus anything nested under it (in both tables), and
    /// never throws when nothing was there to remove.
    /// </summary>
    private sealed class FakeRegistryKeyWrapper : IRegistryKeyWrapper
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(string SubKeyPath, string ValueName), string> _namedValues = new();

        public IReadOnlyDictionary<string, string> Values => _values;

        public IReadOnlyDictionary<(string SubKeyPath, string ValueName), string> NamedValues => _namedValues;

        public int TotalValueCount => _values.Count + _namedValues.Count;

        public void SetDefaultValue(string subKeyPath, string value) => _values[subKeyPath] = value;

        public void SetNamedValue(string subKeyPath, string valueName, string value)
            => _namedValues[(subKeyPath, valueName)] = value;

        public string? GetDefaultValue(string subKeyPath) => _values.GetValueOrDefault(subKeyPath);

        public void DeleteTree(string subKeyPath)
        {
            var prefix = subKeyPath + @"\";
            bool MatchesTree(string path) =>
                string.Equals(path, subKeyPath, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

            foreach (var key in _values.Keys.Where(MatchesTree).ToList())
            {
                _values.Remove(key);
            }

            foreach (var key in _namedValues.Keys.Where(k => MatchesTree(k.SubKeyPath)).ToList())
            {
                _namedValues.Remove(key);
            }
        }
    }
}
