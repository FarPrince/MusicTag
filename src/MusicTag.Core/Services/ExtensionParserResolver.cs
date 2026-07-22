namespace MusicTag.Core.Services;

/// <summary>
/// Maps the 4 alias extensions that ATL.NET doesn't register by extension (even though it
/// has parsers for their underlying codecs/containers) to a canonical extension it does
/// recognize. Confirmed at the ATL source level: <c>new Track(stream, ".mka")</c> routes
/// through ATL's extension-lookup path independent of the real file's name, so resolution
/// happens by opening a <see cref="System.IO.FileStream"/> and constructing the
/// <c>Track</c> with the canonical extension as the "mimeType" parameter — see
/// <see cref="AudioFileService"/>.
/// </summary>
public static class ExtensionParserResolver
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mkv"] = ".mka",
        [".mk3d"] = ".mka",
        [".apl"] = ".ape",
        [".flc"] = ".flac",
    };

    public static bool TryGetCanonicalExtension(string realExtension, out string canonical)
        => Aliases.TryGetValue(realExtension, out canonical!);
}
