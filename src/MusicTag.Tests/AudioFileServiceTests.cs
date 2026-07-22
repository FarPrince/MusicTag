using MusicTag.Core.Models;
using MusicTag.Core.Services;

namespace MusicTag.Tests;

/// <summary>
/// Full write-back round-trip verification for AudioFileService.SaveAsync/SaveManyAsync —
/// closes out plan risk #1's write-back half (M1's ExtensionParserResolverTests only
/// verified reads; SaveAsync didn't exist yet). Each test operates on a throwaway temp copy
/// of the checked-in test-assets sample so repeated runs never mutate the committed
/// fixtures and tests can run in parallel/repeatedly without interference.
/// </summary>
public class AudioFileServiceTests
{
    [Fact]
    public async Task SaveAsync_PlainFlac_RoundTripsMutatedTitle()
    {
        var tempPath = CopyToTemp(TestAssetPaths.FlacSample, ".flac");
        try
        {
            var service = new AudioFileService();

            var loaded = service.Load(tempPath);
            var originalArtist = loaded.PendingFields.Artist;
            var originalAlbum = loaded.PendingFields.Album;

            loaded.PendingFields = loaded.PendingFields with { Title = "Round Trip Title" };
            Assert.True(loaded.IsDirty);

            await service.SaveAsync(loaded);

            // SaveAsync commits pending edits on success (OriginalFields catches up to
            // PendingFields), so the in-memory instance should no longer read as dirty.
            Assert.False(loaded.IsDirty);

            var reloaded = service.Load(tempPath);
            Assert.Equal("Round Trip Title", reloaded.PendingFields.Title);
            Assert.Equal(originalArtist, reloaded.PendingFields.Artist);
            Assert.Equal(originalAlbum, reloaded.PendingFields.Album);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task SaveAsync_FlcAlias_RoundTripsMutatedTitleThroughStreamConstructedTrack()
    {
        var tempPath = CopyToTemp(TestAssetPaths.FlcSample, ".flc");
        try
        {
            var service = new AudioFileService();

            var loaded = service.Load(tempPath);
            loaded.PendingFields = loaded.PendingFields with { Title = "Alias Round Trip" };

            await service.SaveAsync(loaded);

            // Re-open via the same alias mechanism (.flc -> Track(Stream, ".flac")) used at
            // load time, to confirm the write-back landed in the file's real bytes on disk
            // rather than only appearing to succeed in memory — the specific detail plan
            // risk #1 flagged as unverified until this milestone.
            var reloaded = service.Load(tempPath);
            Assert.Equal("Alias Round Trip", reloaded.PendingFields.Title);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task SaveManyAsync_OnlySavesDirtyFiles_AndReportsSucceeded()
    {
        var tempPath = CopyToTemp(TestAssetPaths.FlacSample, ".flac");
        try
        {
            var service = new AudioFileService();

            var dirtyFile = service.Load(tempPath);
            dirtyFile.PendingFields = dirtyFile.PendingFields with { Title = "Batch Saved Title" };

            var cleanFile = service.Load(tempPath); // separate instance, never mutated -> IsDirty == false

            var result = await service.SaveManyAsync(new[] { dirtyFile, cleanFile });

            Assert.Single(result.Succeeded);
            Assert.Same(dirtyFile, result.Succeeded[0]);
            Assert.Empty(result.Failed);

            var reloaded = service.Load(tempPath);
            Assert.Equal("Batch Saved Title", reloaded.PendingFields.Title);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>Minimal valid 1x1 transparent PNG — just needs to be bytes ATL can recognize
    /// and write back as a picture, not a realistic cover image.</summary>
    private static readonly byte[] TinyPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAAAAAA6fptVAAAACklEQVR4nGNgAAIAAAUAAen63NgAAAAASUVORK5CYII=");

    /// <summary>M6: closes out plan section 3's "SaveAsync/SaveManyAsync correctly write
    /// Replaced/Removed album art via ATL's EmbeddedPictures" for the Replaced half. The
    /// checked-in flac sample has no embedded art (confirmed via LoadEmbeddedAlbumArt
    /// returning null before this edit), so this also proves Replaced can add art where there
    /// was none, not just swap an existing picture.</summary>
    [Fact]
    public async Task SaveAsync_ReplacedAlbumArt_RoundTripsEmbeddedPictureBytes()
    {
        var tempPath = CopyToTemp(TestAssetPaths.FlacSample, ".flac");
        try
        {
            var service = new AudioFileService();
            var loaded = service.Load(tempPath);
            Assert.Null(service.LoadEmbeddedAlbumArt(tempPath));

            loaded.PendingAlbumArt = new AlbumArtEdit(AlbumArtAction.Replaced, TinyPngBytes);
            Assert.True(loaded.IsDirty);

            await service.SaveAsync(loaded);

            Assert.False(loaded.IsDirty);
            Assert.Equal(AlbumArtAction.Unchanged, loaded.PendingAlbumArt.Action);

            var reloadedArt = service.LoadEmbeddedAlbumArt(tempPath);
            Assert.NotNull(reloadedArt);
            Assert.Equal(TinyPngBytes, reloadedArt);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>M6: the Removed half of the same write-back contract — starts from a file that
    /// already has embedded art (via a prior Replaced save, since the checked-in sample has
    /// none) and confirms a Removed save actually clears EmbeddedPictures on disk rather than
    /// merely marking the in-memory edit as applied.</summary>
    [Fact]
    public async Task SaveAsync_RemovedAlbumArt_ClearsEmbeddedPictureOnDisk()
    {
        var tempPath = CopyToTemp(TestAssetPaths.FlacSample, ".flac");
        try
        {
            var service = new AudioFileService();

            var withArt = service.Load(tempPath);
            withArt.PendingAlbumArt = new AlbumArtEdit(AlbumArtAction.Replaced, TinyPngBytes);
            await service.SaveAsync(withArt);
            Assert.NotNull(service.LoadEmbeddedAlbumArt(tempPath));

            var toRemove = service.Load(tempPath);
            toRemove.PendingAlbumArt = new AlbumArtEdit(AlbumArtAction.Removed, null);
            Assert.True(toRemove.IsDirty);

            await service.SaveAsync(toRemove);

            Assert.False(toRemove.IsDirty);
            Assert.Null(service.LoadEmbeddedAlbumArt(tempPath));
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static string CopyToTemp(string sourcePath, string extension)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        File.Copy(sourcePath, tempPath);
        return tempPath;
    }

    /// <summary>
    /// M9 build-out: one tiny ffmpeg-generated sample per remaining ffmpeg-encodable format
    /// family (plan section 9's automated set). Every sample was tagged at generation time
    /// with the same Title/Artist/Album triple (see test-assets/README.md for the exact
    /// ffmpeg invocations), so this proves AudioFileService.Load genuinely parses each
    /// container/codec rather than just not throwing.
    ///
    /// .mka is deliberately NOT in this list: investigated directly (not guessed) by diffing
    /// ffprobe's view of the container against AudioFileService.Load's output, ffmpeg's
    /// Matroska muxer places the global "title"/"album" metadata keys in a spot ATL's
    /// Matroska reader does not consult (Title falls back to the filename; Album reads
    /// empty), while "artist" happens to land somewhere ATL does read. This reproduces
    /// identically whether the tags are written at container level or per-stream level, so
    /// it is a real ffmpeg/ATL Matroska-container quirk in the *source* file, not a bug in
    /// AudioFileService: writing tags through ATL itself (SaveAsync) and reading them back
    /// works correctly for every field, as proven by
    /// <see cref="SaveAsync_Mka_RoundTripsMutatedTitleAndAlbum"/> below and by the
    /// canonical-vs-alias equality assertions in ExtensionParserResolverTests (which compare
    /// two reads of the same underlying quirky value rather than asserting a hardcoded
    /// literal). MKA/MKV/MK3D alias read coverage lives entirely in
    /// ExtensionParserResolverTests instead.
    /// </summary>
    public static IEnumerable<object[]> AllFormatSamples()
    {
        yield return new object[] { "MP3", TestAssetPaths.Mp3Sample, ".mp3" };
        yield return new object[] { "OGG", TestAssetPaths.OggSample, ".ogg" };
        yield return new object[] { "Opus", TestAssetPaths.OpusSample, ".opus" };
        yield return new object[] { "M4A", TestAssetPaths.M4aSample, ".m4a" };
        yield return new object[] { "WMA", TestAssetPaths.WmaSample, ".wma" };
        yield return new object[] { "WAV", TestAssetPaths.WavSample, ".wav" };
        yield return new object[] { "WV", TestAssetPaths.WvSample, ".wv" };
    }

    /// <summary>Dedicated MKA mutate/save/reload round-trip (plan section 9 requires one per
    /// format, MKA included) — deliberately does not assert anything about the pristine
    /// file's original Title/Album (see AllFormatSamples' doc comment on why those read back
    /// oddly pre-save), only that a field this test itself writes via SaveAsync reads back
    /// correctly afterwards, which is the actual round-trip contract SaveAsync must honor.</summary>
    [Fact]
    public async Task SaveAsync_Mka_RoundTripsMutatedTitleAndAlbum()
    {
        var tempPath = CopyToTemp(TestAssetPaths.MkaSample, ".mka");
        try
        {
            var service = new AudioFileService();
            var loaded = service.Load(tempPath);
            var originalArtist = loaded.PendingFields.Artist;

            loaded.PendingFields = loaded.PendingFields with { Title = "MKA Round Trip", Album = "MKA Album RT" };
            Assert.True(loaded.IsDirty);

            await service.SaveAsync(loaded);

            Assert.False(loaded.IsDirty);

            var reloaded = service.Load(tempPath);
            Assert.Equal("MKA Round Trip", reloaded.PendingFields.Title);
            Assert.Equal("MKA Album RT", reloaded.PendingFields.Album);
            Assert.Equal(originalArtist, reloaded.PendingFields.Artist);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Theory]
    [MemberData(nameof(AllFormatSamples))]
    public void Load_ReadsEmbeddedTags_ForEachFormat(string label, string samplePath, string extension)
    {
        Assert.True(File.Exists(samplePath), $"Missing test asset for {label}: {samplePath}");

        var service = new AudioFileService();
        var loaded = service.Load(samplePath);

        Assert.Equal("Test Title", loaded.PendingFields.Title);
        Assert.Equal("Test Artist", loaded.PendingFields.Artist);
        Assert.Equal("Test Album", loaded.PendingFields.Album);
        Assert.Equal(Path.GetFileName(samplePath), loaded.FileName);
        Assert.True(loaded.ExtendedInfo.Duration > TimeSpan.Zero, $"{label}: expected a non-zero decoded duration.");
        _ = extension; // kept for readable Theory display names / future per-extension branching
    }

    [Theory]
    [MemberData(nameof(AllFormatSamples))]
    public async Task SaveAsync_RoundTripsMutatedTitle_ForEachFormat(string label, string samplePath, string extension)
    {
        var tempPath = CopyToTemp(samplePath, extension);
        try
        {
            var service = new AudioFileService();
            var loaded = service.Load(tempPath);
            var originalArtist = loaded.PendingFields.Artist;

            loaded.PendingFields = loaded.PendingFields with { Title = $"{label} Round Trip" };
            Assert.True(loaded.IsDirty);

            await service.SaveAsync(loaded);

            Assert.False(loaded.IsDirty);

            var reloaded = service.Load(tempPath);
            Assert.Equal($"{label} Round Trip", reloaded.PendingFields.Title);
            Assert.Equal(originalArtist, reloaded.PendingFields.Artist);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>Regression test for a real bug found via manual testing: clearing a text field
    /// down to nothing (the app's model represents "no value" as null — see
    /// EditPanelViewModel/FileListItemViewModel's Normalize()) silently failed to persist, because
    /// ATL treats a null string tag value as "leave whatever's on disk alone" rather than "erase
    /// it". Fixed in AudioFileService.ApplyPendingFields by coercing null to "" right before
    /// handing values to ATL. This test first gives the file a real Comment, confirms it wrote,
    /// then clears it and confirms the clear also actually persisted — not just that setting a
    /// *new* non-empty value works, which every other round-trip test here already covered.</summary>
    [Fact]
    public async Task SaveAsync_ClearedComment_ActuallyPersistsAsEmpty()
    {
        var tempPath = CopyToTemp(TestAssetPaths.FlacSample, ".flac");
        try
        {
            var service = new AudioFileService();

            var loaded = service.Load(tempPath);
            loaded.PendingFields = loaded.PendingFields with { Comment = "A real comment" };
            await service.SaveAsync(loaded);

            var withComment = service.Load(tempPath);
            Assert.Equal("A real comment", withComment.PendingFields.Comment);

            // Mirrors the app's own Normalize(): an empty/cleared text field is represented as
            // null in PendingFields, exactly what EditPanelViewModel/FileListItemViewModel would
            // commit when a user deletes all the text in the Comment box.
            withComment.PendingFields = withComment.PendingFields with { Comment = null };
            await service.SaveAsync(withComment);

            var reloaded = service.Load(tempPath);
            Assert.True(string.IsNullOrEmpty(reloaded.PendingFields.Comment),
                $"Expected Comment to be cleared, but it was \"{reloaded.PendingFields.Comment}\".");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
