namespace MusicTag.Core.Services;

public sealed class LyricsSearchService : ILyricsSearchService
{
    // LRCLib is a shared public API we don't control, not our own infrastructure — bounded
    // rather than firing every file's request at once, but still enough to make a
    // network-round-trip-per-file search meaningfully faster than one-at-a-time.
    private const int MaxConcurrency = 6;

    private readonly IAudioFileService _audioFileService;
    private readonly ILrcLibClient _lrcLibClient;

    public LyricsSearchService(IAudioFileService audioFileService, ILrcLibClient lrcLibClient)
    {
        _audioFileService = audioFileService;
        _lrcLibClient = lrcLibClient;
    }

    public async Task<LyricsSearchResult> SearchAsync(
        IReadOnlyList<string> directories, IProgress<LyricsFileResult>? progress = null, CancellationToken ct = default)
    {
        var files = EnumerateAudioFiles(directories);
        var total = files.Count;

        var downloaded = 0;
        var alreadyHadLyrics = 0;
        var noMatch = 0;
        var errors = 0;
        var completed = 0;

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = ct },
            async (fullPath, fileCt) =>
            {
                var outcome = await ProcessFileAsync(fullPath, fileCt).ConfigureAwait(false);

                switch (outcome)
                {
                    case LyricsFileOutcome.Downloaded: Interlocked.Increment(ref downloaded); break;
                    case LyricsFileOutcome.AlreadyHadLyrics: Interlocked.Increment(ref alreadyHadLyrics); break;
                    case LyricsFileOutcome.NoMatch: Interlocked.Increment(ref noMatch); break;
                    case LyricsFileOutcome.Error: Interlocked.Increment(ref errors); break;
                }

                var current = Interlocked.Increment(ref completed);
                progress?.Report(new LyricsFileResult(Path.GetFileName(fullPath), outcome, current, total));
            }).ConfigureAwait(false);

        return new LyricsSearchResult(downloaded, alreadyHadLyrics, noMatch, errors);
    }

    private async Task<LyricsFileOutcome> ProcessFileAsync(string fullPath, CancellationToken ct)
    {
        // Never overwrite an existing .lrc — per user request, this feature only fills in
        // songs that don't have one yet.
        var lrcPath = Path.ChangeExtension(fullPath, ".lrc");
        if (File.Exists(lrcPath))
        {
            return LyricsFileOutcome.AlreadyHadLyrics;
        }

        try
        {
            var audioFile = _audioFileService.Load(fullPath);
            var fields = audioFile.OriginalFields;

            string artist, title;
            if (!string.IsNullOrWhiteSpace(fields.Artist) && !string.IsNullOrWhiteSpace(fields.Title))
            {
                artist = fields.Artist;
                title = fields.Title;
            }
            else if (!LyricsFilenameParser.TryParseArtistAndTitle(
                         Path.GetFileNameWithoutExtension(fullPath), out artist, out title))
            {
                // No usable tags and the filename doesn't follow "Artist - Title" either —
                // nothing sensible to query LRCLib with.
                return LyricsFileOutcome.NoMatch;
            }

            var durationSeconds = (int)Math.Round(audioFile.ExtendedInfo.Duration.TotalSeconds);
            var track = await _lrcLibClient.GetAsync(artist, title, fields.Album, durationSeconds, ct)
                .ConfigureAwait(false);

            // Per user request: only ever write synced lyrics — a LRCLib hit with just
            // plain lyrics (or an instrumental track, which never has synced lyrics) is
            // treated the same as no match at all.
            if (track is null || track.Instrumental || string.IsNullOrEmpty(track.SyncedLyrics))
            {
                return LyricsFileOutcome.NoMatch;
            }

            await File.WriteAllTextAsync(lrcPath, track.SyncedLyrics, ct).ConfigureAwait(false);
            return LyricsFileOutcome.Downloaded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One file's network/IO/tag-read failure must not abort the whole library
            // search — isolate per-file, same as AudioFileService.SaveManyAsync.
            return LyricsFileOutcome.Error;
        }
    }

    private static List<string> EnumerateAudioFiles(IReadOnlyList<string> directories)
    {
        var files = new List<string>();

        foreach (var directory in directories)
        {
            // A configured directory that's since been renamed/removed/unmounted is silently
            // skipped rather than failing the whole search over one bad entry.
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                // Only files with a supported audio extension are ever considered — every
                // other file the directory walk encounters (art, playlists, .lrc files
                // themselves, etc.) is skipped here, before any tag read or network call.
                if (SupportedExtensions.IsSupported(Path.GetExtension(file)))
                {
                    files.Add(file);
                }
            }
        }

        return files;
    }
}
