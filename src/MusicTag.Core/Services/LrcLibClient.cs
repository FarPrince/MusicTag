using System.Net;
using System.Net.Http;
using System.Text.Json;
using MusicTag.Core.Models;

namespace MusicTag.Core.Services;

public sealed class LrcLibClient : ILrcLibClient, IDisposable
{
    private readonly HttpClient _httpClient;

    public LrcLibClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://lrclib.net"),
            Timeout = TimeSpan.FromSeconds(15),
        };

        // LRCLib asks API consumers to identify themselves via User-Agent rather than showing
        // up as an anonymous client.
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "MusicTag/1.4 (+https://github.com/FarPrince/MP3Tag)");
    }

    public async Task<LrcLibTrack?> GetAsync(
        string artistName, string trackName, string? albumName, int? durationSeconds, CancellationToken ct = default)
    {
        var query = $"/api/get?track_name={Uri.EscapeDataString(trackName)}&artist_name={Uri.EscapeDataString(artistName)}";

        if (!string.IsNullOrWhiteSpace(albumName))
        {
            query += $"&album_name={Uri.EscapeDataString(albumName)}";
        }

        if (durationSeconds is > 0)
        {
            query += $"&duration={durationSeconds.Value}";
        }

        using var response = await _httpClient.GetAsync(query, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = document.RootElement;

        return new LrcLibTrack(
            SyncedLyrics: GetOptionalString(root, "syncedLyrics"),
            PlainLyrics: GetOptionalString(root, "plainLyrics"),
            Instrumental: root.TryGetProperty("instrumental", out var instrumental)
                && instrumental.ValueKind == JsonValueKind.True);
    }

    private static string? GetOptionalString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public void Dispose() => _httpClient.Dispose();
}
