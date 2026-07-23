namespace MusicTag.Core.Models;

/// <summary>
/// Backs the toolbar's "Normalize Separators" button — per user request, replaces ";"-separated
/// multi-value tag text (e.g. "Rock;Pop") with ", "-separated text ("Rock, Pop") in whichever of
/// the 7 string <see cref="TagFieldSet"/> fields the user has enabled via
/// <see cref="Settings.AppSettings.SeparatorNormalizationFields"/>. A pure, WPF-free transform
/// (like the rest of MusicTag.Core) so it's directly unit-testable and reusable if a future CLI
/// tool wants the same behavior.
/// </summary>
public static class SeparatorNormalization
{
    /// <summary>Every string field eligible for this transform, in the same order
    /// SettingsWindow.xaml lists its checkboxes. Year/TrackNumber/DiscNumber are ints and have
    /// no separator concept, so they're excluded entirely.</summary>
    public static readonly IReadOnlyList<string> FieldNames =
        ["Title", "Album", "Artist", "AlbumArtist", "Comment", "Composer", "Genre"];

    /// <summary>Splits on ';', trims each part, and drops empty parts before rejoining with
    /// ", " — this normalizes inconsistent existing spacing (e.g. "Rock; Pop" or "Rock ;Pop")
    /// rather than a naive string.Replace, which would leave stray whitespace in those cases.
    /// Returns the original value unchanged when there's no ';' to begin with (including
    /// null/empty), so callers can cheaply tell "nothing changed" via reference/value equality.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains(';'))
            return value;

        var parts = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>Applies <see cref="Normalize"/> to exactly the fields named in
    /// <paramref name="enabledFields"/>, leaving every other field untouched. Returns a new
    /// <see cref="TagFieldSet"/> (or the same values wrapped in a new record when nothing
    /// actually changed — callers compare against the original via TagFieldSet's record
    /// equality, same pattern EditPanelViewModel.CommitField already uses).</summary>
    public static TagFieldSet Apply(TagFieldSet fields, IReadOnlySet<string> enabledFields) => fields with
    {
        Title = enabledFields.Contains("Title") ? Normalize(fields.Title) : fields.Title,
        Album = enabledFields.Contains("Album") ? Normalize(fields.Album) : fields.Album,
        Artist = enabledFields.Contains("Artist") ? Normalize(fields.Artist) : fields.Artist,
        AlbumArtist = enabledFields.Contains("AlbumArtist") ? Normalize(fields.AlbumArtist) : fields.AlbumArtist,
        Comment = enabledFields.Contains("Comment") ? Normalize(fields.Comment) : fields.Comment,
        Composer = enabledFields.Contains("Composer") ? Normalize(fields.Composer) : fields.Composer,
        Genre = enabledFields.Contains("Genre") ? Normalize(fields.Genre) : fields.Genre,
    };
}
