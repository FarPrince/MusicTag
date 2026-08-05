using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MusicTag.Core.Integration;

/// <summary>
/// Real <see cref="ILinuxDesktopFileWriter"/> backed by <c>System.IO</c> plus a
/// <c>chmod</c> P/Invoke for the executable bit (.NET has no managed API for POSIX permission
/// bits) — the Linux counterpart of <see cref="RegistryKeyWrapper"/>. Same
/// <see cref="System.Runtime.Versioning.SupportedOSPlatformAttribute"/> treatment: <c>MusicTag.Core</c>
/// itself stays plain <c>net8.0</c> for portability/testability, but this one class is inherently
/// Linux-only.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxDesktopFileWriter : ILinuxDesktopFileWriter
{
    // mode_t 0755 (rwxr-xr-x) — matches the permission bits a normal `chmod +x` would set;
    // no managed System.IO API can set the executable bit, hence the direct libc P/Invoke.
    private const int ExecutableMode = 0b111_101_101;

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string pathname, int mode);

    public string GetHomeDirectory() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public bool FileExists(string path) => File.Exists(path);

    public void WriteFile(string path, string content, bool executable)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);

        if (executable)
        {
            chmod(path, ExecutableMode);
        }
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
