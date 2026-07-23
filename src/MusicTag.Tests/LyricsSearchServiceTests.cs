using System.Collections.Concurrent;
using MusicTag.Core.Models;
using MusicTag.Core.Services;

namespace MusicTag.Tests;

/// <summary>
/// Exercises LyricsSearchService's per-file decision logic (already-has-lyrics skip, tag vs.
/// filename-fallback matching, synced-vs-plain/instrumental filtering, per-file error
/// isolation) against fakes for IAudioFileService/ILrcLibClient — real temp files/directories
/// so the actual Directory.EnumerateFiles/File.Exists/File.WriteAllTextAsync disk-I/O paths
/// are genuinely covered, without ever hitting the real LRCLib API or ATL.
/// </summary>
public class LyricsSearchServiceTests : IDisposable
{
    private readonly string _tempDir;

    public LyricsSearchServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MusicTagLyricsTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateAudioFile(string fileName)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, "not a real audio file — Load() is faked in these tests");
        return path;
    }

    private static AudioFile MakeAudioFile(string? artist, string? title, string? album = null)
        => new(
            "C:\\music",
            "irrelevant.mp3",
            new TagFieldSet { Artist = artist, Title = title, Album = album },
            new ExtendedAudioInfo("MP3", 320, 44100, 2, TimeSpan.FromSeconds(200), 0, false, "ID3v2", DateTime.UtcNow));

    [Fact]
    public async Task SearchAsync_SkipsFilesThatAlreadyHaveAnLrc()
    {
        var songPath = CreateAudioFile("song.mp3");
        File.WriteAllText(Path.ChangeExtension(songPath, ".lrc"), "[00:00.00]existing lyrics");

        var audioFileService = new FakeAudioFileService();
        var lrcLibClient = new FakeLrcLibClient();
        var service = new LyricsSearchService(audioFileService, lrcLibClient);

        var result = await service.SearchAsync([_tempDir]);

        Assert.Equal(1, result.AlreadyHadLyrics);
        Assert.Equal(0, result.Downloaded);
        Assert.Empty(lrcLibClient.Queries); // Never even queried — already-has-lyrics short-circuits first.
    }

    [Fact]
    public async Task SearchAsync_UsesTags_DownloadsSyncedLyrics_WhenLrcLibHasAMatch()
    {
        var songPath = CreateAudioFile("track01.mp3");

        var audioFileService = new FakeAudioFileService();
        audioFileService.Files[songPath] = MakeAudioFile("The Beatles", "Yesterday", "Help!");

        var lrcLibClient = new FakeLrcLibClient();
        lrcLibClient.Results[("The Beatles", "Yesterday")] = new LrcLibTrack("[00:00.00]Yesterday...", "Yesterday...", Instrumental: false);

        var service = new LyricsSearchService(audioFileService, lrcLibClient);
        var result = await service.SearchAsync([_tempDir]);

        Assert.Equal(1, result.Downloaded);
        Assert.Equal(0, result.NoMatch);
        Assert.Equal("[00:00.00]Yesterday...", await File.ReadAllTextAsync(Path.ChangeExtension(songPath, ".lrc")));
    }

    [Fact]
    public async Task SearchAsync_FallsBackToFilename_WhenTagsAreMissing()
    {
        var songPath = CreateAudioFile("James Blake - When I'm Home.mp3");

        var audioFileService = new FakeAudioFileService();
        audioFileService.Files[songPath] = MakeAudioFile(artist: null, title: null);

        var lrcLibClient = new FakeLrcLibClient();
        lrcLibClient.Results[("James Blake", "When I'm Home")] = new LrcLibTrack("[00:00.00]synced", null, Instrumental: false);

        var service = new LyricsSearchService(audioFileService, lrcLibClient);
        var result = await service.SearchAsync([_tempDir]);

        Assert.Equal(1, result.Downloaded);
        Assert.Contains(("James Blake", "When I'm Home"), lrcLibClient.Queries);
    }

    [Fact]
    public async Task SearchAsync_ReportsNoMatch_WhenTagsAndFilenameBothUnusable()
    {
        var audioFileService = new FakeAudioFileService();
        var songPath = CreateAudioFile("track01.mp3"); // No " - " separator.
        audioFileService.Files[songPath] = MakeAudioFile(artist: null, title: null);

        var lrcLibClient = new FakeLrcLibClient();
        var service = new LyricsSearchService(audioFileService, lrcLibClient);

        var result = await service.SearchAsync([_tempDir]);

        Assert.Equal(1, result.NoMatch);
        Assert.Empty(lrcLibClient.Queries); // Never queried — nothing usable to send.
    }

    [Theory]
    [MemberData(nameof(UnusableLrcLibResults))]
    public async Task SearchAsync_ReportsNoMatch_ForInstrumentalOrPlainOnlyOrMissingResults(LrcLibTrack? track)
    {
        var songPath = CreateAudioFile("song.mp3");
        var audioFileService = new FakeAudioFileService();
        audioFileService.Files[songPath] = MakeAudioFile("Artist", "Title");

        var lrcLibClient = new FakeLrcLibClient();
        lrcLibClient.Results[("Artist", "Title")] = track;

        var service = new LyricsSearchService(audioFileService, lrcLibClient);
        var result = await service.SearchAsync([_tempDir]);

        Assert.Equal(1, result.NoMatch);
        Assert.Equal(0, result.Downloaded);
        Assert.False(File.Exists(Path.ChangeExtension(songPath, ".lrc")));
    }

    public static IEnumerable<object?[]> UnusableLrcLibResults()
    {
        yield return [null]; // LRCLib has nothing for this track at all.
        yield return [new LrcLibTrack(SyncedLyrics: null, PlainLyrics: "plain only", Instrumental: false)];
        yield return [new LrcLibTrack(SyncedLyrics: "[00:00.00]synced", PlainLyrics: null, Instrumental: true)];
    }

    [Fact]
    public async Task SearchAsync_IsolatesPerFileErrors_AndKeepsProcessingOtherFiles()
    {
        var brokenPath = CreateAudioFile("broken.mp3");
        var okPath = CreateAudioFile("ok.mp3");

        var audioFileService = new FakeAudioFileService();
        audioFileService.LoadFailures.Add(brokenPath);
        audioFileService.Files[okPath] = MakeAudioFile("Artist", "Title");

        var lrcLibClient = new FakeLrcLibClient();
        lrcLibClient.Results[("Artist", "Title")] = new LrcLibTrack("[00:00.00]synced", null, false);

        var service = new LyricsSearchService(audioFileService, lrcLibClient);
        var result = await service.SearchAsync([_tempDir]);

        Assert.Equal(1, result.Errors);
        Assert.Equal(1, result.Downloaded);
    }

    [Fact]
    public async Task SearchAsync_SkipsNonExistentDirectories_WithoutThrowing()
    {
        var audioFileService = new FakeAudioFileService();
        var lrcLibClient = new FakeLrcLibClient();
        var service = new LyricsSearchService(audioFileService, lrcLibClient);

        var result = await service.SearchAsync([Path.Combine(_tempDir, "does-not-exist")]);

        Assert.Equal(new LyricsSearchResult(0, 0, 0, 0), result);
    }

    private sealed class FakeAudioFileService : IAudioFileService
    {
        public Dictionary<string, AudioFile> Files { get; } = new();
        public HashSet<string> LoadFailures { get; } = new();

        public AudioFile Load(string fullPath)
        {
            if (LoadFailures.Contains(fullPath))
                throw new IOException($"Simulated load failure for {fullPath}");

            return Files.TryGetValue(fullPath, out var file)
                ? file
                : throw new InvalidOperationException($"Test bug: no fake AudioFile registered for {fullPath}");
        }

        public byte[]? LoadEmbeddedAlbumArt(string fullPath) => throw new NotSupportedException();
        public Task SaveAsync(AudioFile file, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<BatchSaveResult> SaveManyAsync(IEnumerable<AudioFile> files, IProgress<BatchSaveProgress>? progress = null, CancellationToken ct = default)
            => throw new NotSupportedException();
        public void Rename(AudioFile file, string newFileName) => throw new NotSupportedException();
    }

    [Fact]
    public async Task SearchAsync_ProcessesMultipleFilesConcurrently_AndAggregatesCorrectly()
    {
        var audioFileService = new FakeAudioFileService();
        var lrcLibClient = new FakeLrcLibClient();

        const int fileCount = 12;
        for (var i = 0; i < fileCount; i++)
        {
            var songPath = CreateAudioFile($"track{i:D2}.mp3");
            audioFileService.Files[songPath] = MakeAudioFile($"Artist{i}", $"Title{i}");
            lrcLibClient.Results[($"Artist{i}", $"Title{i}")] = new LrcLibTrack($"[00:00.00]synced{i}", null, false);
        }

        var service = new LyricsSearchService(audioFileService, lrcLibClient);

        var result = await service.SearchAsync([_tempDir]);

        Assert.Equal(fileCount, result.Downloaded);
        Assert.Equal(fileCount, lrcLibClient.Queries.Count);
        for (var i = 0; i < fileCount; i++)
        {
            Assert.True(File.Exists(Path.Combine(_tempDir, $"track{i:D2}.lrc")));
        }
    }

    private sealed class FakeLrcLibClient : ILrcLibClient
    {
        public ConcurrentDictionary<(string Artist, string Title), LrcLibTrack?> Results { get; } = new();
        public ConcurrentBag<(string Artist, string Title)> Queries { get; } = new();

        public Task<LrcLibTrack?> GetAsync(string artistName, string trackName, string? albumName, int? durationSeconds, CancellationToken ct = default)
        {
            var key = (artistName, trackName);
            Queries.Add(key);
            return Task.FromResult(Results.GetValueOrDefault(key));
        }
    }
}
