namespace MusicTag.Tests;

/// <summary>
/// Shared repo-root/test-asset path resolution used by both ExtensionParserResolverTests
/// (read-path alias verification, M1) and AudioFileServiceTests (write-back round-trip,
/// M2) so the two test files don't duplicate the same directory-walk logic.
/// </summary>
internal static class TestAssetPaths
{
    public static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MusicTag.sln")))
            {
                dir = dir.Parent;
            }

            if (dir is null)
                throw new InvalidOperationException("Could not locate repo root (MusicTag.sln) from test output directory.");

            return dir.FullName;
        }
    }

    public static string FlacSample => Path.Combine(RepoRoot, "test-assets", "flac", "silence_tagged.flac");

    public static string FlcSample => Path.Combine(RepoRoot, "test-assets", "flac", "silence_tagged.flc");

    // M9 build-out: one tiny (~1 second) ffmpeg-generated sample per remaining
    // ffmpeg-capable format family, per plan section 9. See test-assets/README.md for
    // exact generation commands/provenance.
    public static string Mp3Sample => Path.Combine(RepoRoot, "test-assets", "mp3", "silence_tagged.mp3");

    public static string OggSample => Path.Combine(RepoRoot, "test-assets", "ogg", "silence_tagged.ogg");

    public static string OpusSample => Path.Combine(RepoRoot, "test-assets", "opus", "silence_tagged.opus");

    public static string M4aSample => Path.Combine(RepoRoot, "test-assets", "m4a", "silence_tagged.m4a");

    public static string WmaSample => Path.Combine(RepoRoot, "test-assets", "wma", "silence_tagged.wma");

    public static string WavSample => Path.Combine(RepoRoot, "test-assets", "wav", "silence_tagged.wav");

    public static string WvSample => Path.Combine(RepoRoot, "test-assets", "wv", "silence_tagged.wv");

    // .mka is natively registered by ATL (canonical); .mkv/.mk3d are byte-for-byte copies
    // used to exercise ExtensionParserResolver's alias path, the same way flc mirrors flac.
    public static string MkaSample => Path.Combine(RepoRoot, "test-assets", "mka", "silence_tagged.mka");

    public static string MkvSample => Path.Combine(RepoRoot, "test-assets", "mkv", "silence_tagged.mkv");

    public static string Mk3dSample => Path.Combine(RepoRoot, "test-assets", "mk3d", "silence_tagged.mk3d");
}
