using MusicTag.Core.Settings;
using AppClass = MusicTag.App.App;

namespace MusicTag.Tests;

/// <summary>
/// Covers App.ResolveStartupFolder's file/multi-select handling (plan section 4): a Nautilus
/// "Open with MusicTag" invocation on one or more selected audio files (rather than a folder)
/// must resolve to the containing directory, since Windows never offers this menu entry on an
/// individual file but Nautilus does. Uses real temp files/directories rather than mocking
/// Directory.Exists/File.Exists, since ResolveStartupFolder calls those statically.
/// </summary>
public class AppResolveStartupFolderTests
{
    [Fact]
    public void SingleFileArg_ResolvesToContainingDirectory()
    {
        var (dir, filePath) = CreateTempFileInNewDir();
        try
        {
            var result = AppClass.ResolveStartupFolder([filePath], new AppSettings());
            Assert.Equal(dir, result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MultipleFileArgs_ResolvesToFirstItemsContainingDirectory()
    {
        var (dir, filePath) = CreateTempFileInNewDir();
        var secondFilePath = Path.Combine(dir, "second.mp3");
        File.WriteAllText(secondFilePath, string.Empty);
        try
        {
            var result = AppClass.ResolveStartupFolder([filePath, secondFilePath], new AppSettings());
            Assert.Equal(dir, result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DirectoryArg_ResolvesToThatDirectory_Unchanged()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var result = AppClass.ResolveStartupFolder([dir], new AppSettings());
            Assert.Equal(dir, result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void NoMatchingArg_FallsBackToDefaultStartupFolder()
    {
        var defaultDir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(defaultDir);
        try
        {
            var result = AppClass.ResolveStartupFolder(
                ["/nonexistent/path/that/does/not/exist"],
                new AppSettings { DefaultStartupFolder = defaultDir });

            Assert.Equal(defaultDir, result);
        }
        finally
        {
            Directory.Delete(defaultDir, recursive: true);
        }
    }

    [Fact]
    public void NoArgsAndNoDefault_ReturnsNull()
    {
        var result = AppClass.ResolveStartupFolder([], new AppSettings());
        Assert.Null(result);
    }

    private static (string Directory, string FilePath) CreateTempFileInNewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "song.mp3");
        File.WriteAllText(filePath, string.Empty);
        return (dir, filePath);
    }
}
