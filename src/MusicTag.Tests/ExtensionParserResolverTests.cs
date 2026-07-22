using MusicTag.Core.Services;

namespace MusicTag.Tests;

/// <summary>
/// Verifies plan risk #1's read-back half: that ATL.NET's stream-constructed
/// <c>Track(Stream, mimeType)</c> path (used for the .mkv/.mk3d/.apl/.flc alias extensions
/// ATL doesn't register natively) actually reads tags correctly through
/// <see cref="AudioFileService.Load"/>. The write-back half (mutate a field, SaveAsync,
/// reload, assert the change persisted) lives in AudioFileServiceTests, added in M2 once
/// SaveAsync existed.
///
/// Sample provenance: test-assets/flac/silence_tagged.flac is a ~1 second silent FLAC
/// generated locally with ffmpeg (`ffmpeg -f lavfi -i anullsrc=... -c:a flac -metadata
/// title=... ...`), tagged with Title/Artist/Album, then byte-for-byte copied to
/// silence_tagged.flc to exercise the alias path. Broader test-assets/ build-out
/// (additional formats, licensing/provenance README) is M9's responsibility.
/// </summary>
public class ExtensionParserResolverTests
{
    [Fact]
    public void Load_FlcAlias_ReadsSameTagsAsCanonicalFlac()
    {
        var flacPath = TestAssetPaths.FlacSample;
        var flcPath = TestAssetPaths.FlcSample;

        Assert.True(File.Exists(flacPath), $"Missing test asset: {flacPath}");
        Assert.True(File.Exists(flcPath), $"Missing test asset: {flcPath}");

        var service = new AudioFileService();

        // Canonical .flac loads via the plain new Track(fullPath) path.
        var canonical = service.Load(flacPath);

        // .flc is not registered by ATL — this must go through ExtensionParserResolver
        // (.flc -> .flac) and ATL's Track(Stream, ".flac") constructor.
        var aliased = service.Load(flcPath);

        Assert.Equal(canonical.PendingFields.Title, aliased.PendingFields.Title);
        Assert.Equal(canonical.PendingFields.Artist, aliased.PendingFields.Artist);
        Assert.Equal(canonical.PendingFields.Album, aliased.PendingFields.Album);

        // Concrete, non-null assertions against what the ffmpeg-authored sample actually
        // contains, so this test would fail loudly if the alias path silently returned an
        // empty/default TagFieldSet instead of genuinely parsing the stream.
        Assert.Equal("Test Title", aliased.PendingFields.Title);
        Assert.Equal("Test Artist", aliased.PendingFields.Artist);
        Assert.Equal("Test Album", aliased.PendingFields.Album);
    }

    /// <summary>
    /// M9 build-out: the second of the four documented alias extensions (.mkv -> .mka),
    /// exercised the same way as .flc -> .flac above but against a real Matroska-audio
    /// sample (ffmpeg-generated FLAC-in-Matroska), proving the alias mechanism isn't
    /// accidentally FLAC-specific. Sample provenance: test-assets/README.md.
    /// </summary>
    [Fact]
    public void Load_MkvAlias_ReadsSameTagsAsCanonicalMka()
    {
        var mkaPath = TestAssetPaths.MkaSample;
        var mkvPath = TestAssetPaths.MkvSample;

        Assert.True(File.Exists(mkaPath), $"Missing test asset: {mkaPath}");
        Assert.True(File.Exists(mkvPath), $"Missing test asset: {mkvPath}");

        var service = new AudioFileService();

        // Canonical .mka is natively registered by ATL -> plain new Track(fullPath) path.
        var canonical = service.Load(mkaPath);

        // .mkv is not registered by ATL -> must go through ExtensionParserResolver
        // (.mkv -> .mka) and ATL's Track(Stream, ".mka") constructor.
        var aliased = service.Load(mkvPath);

        // Artist/Album are asserted for equality between the two read paths (Album is
        // empty for both — see below — but Artist is a concrete non-empty value, so this
        // still proves the alias path genuinely parses the stream rather than silently
        // returning an empty/default TagFieldSet).
        Assert.Equal(canonical.PendingFields.Artist, aliased.PendingFields.Artist);
        Assert.Equal(canonical.PendingFields.Album, aliased.PendingFields.Album);
        Assert.Equal("Test Artist", aliased.PendingFields.Artist);

        // Title is deliberately NOT compared for equality here, and this is investigated,
        // not guessed: two independent quirks compound for this one field specifically.
        // (1) ffmpeg's Matroska muxer places the global "title" tag somewhere ATL's
        // Matroska reader doesn't consult, so ATL falls back to deriving Title from the
        // *filename* instead — confirmed directly via ffprobe against the raw container
        // (see AudioFileServiceTests.AllFormatSamples' doc comment). (2) that
        // filename-derived fallback requires ATL to know the real filename, which the
        // canonical path has (constructed via Track(fullPath), so Title reads back as
        // "silence_tagged") but the stream-constructed alias path does not (Track(Stream,
        // ".mka") has no filename to fall back to, so Title reads back empty). This
        // divergence is an artifact of the fallback depending on path-vs-stream
        // construction — not evidence the alias mechanism mis-resolved or mis-parsed
        // anything (Artist/Album, which don't hit this fallback, agree exactly above).
        Assert.Equal("silence_tagged", canonical.PendingFields.Title);
        Assert.Equal(string.Empty, aliased.PendingFields.Title);
    }

    /// <summary>Third alias extension, .mk3d -> .mka, same shape as the .mkv test above
    /// (including the Title fallback divergence documented there).</summary>
    [Fact]
    public void Load_Mk3dAlias_ReadsSameTagsAsCanonicalMka()
    {
        var mkaPath = TestAssetPaths.MkaSample;
        var mk3dPath = TestAssetPaths.Mk3dSample;

        Assert.True(File.Exists(mkaPath), $"Missing test asset: {mkaPath}");
        Assert.True(File.Exists(mk3dPath), $"Missing test asset: {mk3dPath}");

        var service = new AudioFileService();
        var canonical = service.Load(mkaPath);
        var aliased = service.Load(mk3dPath);

        Assert.Equal(canonical.PendingFields.Artist, aliased.PendingFields.Artist);
        Assert.Equal(canonical.PendingFields.Album, aliased.PendingFields.Album);
        Assert.Equal("Test Artist", aliased.PendingFields.Artist);
    }

    /// <summary>
    /// M9 build-out: write-back through the .mkv alias, mirroring
    /// AudioFileServiceTests' SaveAsync_FlcAlias_RoundTripsMutatedTitleThroughStreamConstructedTrack
    /// for the second alias family -- proves Save() genuinely persists through the
    /// stream-constructed Track for Matroska too, not just FLAC-in-a-different-extension.
    /// </summary>
    [Fact]
    public async Task SaveAsync_MkvAlias_RoundTripsMutatedTitleThroughStreamConstructedTrack()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mkv");
        File.Copy(TestAssetPaths.MkvSample, tempPath);
        try
        {
            var service = new AudioFileService();

            var loaded = service.Load(tempPath);
            loaded.PendingFields = loaded.PendingFields with { Title = "Mkv Alias Round Trip" };

            await service.SaveAsync(loaded);

            var reloaded = service.Load(tempPath);
            Assert.Equal("Mkv Alias Round Trip", reloaded.PendingFields.Title);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
