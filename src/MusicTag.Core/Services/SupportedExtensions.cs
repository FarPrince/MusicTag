namespace MusicTag.Core.Services;

/// <summary>
/// The full set of audio/container file extensions MusicTag treats as tag-editable,
/// including several obscure lossless codecs (APE, MPC, WV, TTA, TAK, OptimFROG) whose
/// APEv2 tag engine ATL.NET shares internally, plus the 4 alias extensions
/// (.mkv/.mk3d/.apl/.flc) that ATL can decode but doesn't register by extension —
/// see <see cref="ExtensionParserResolver"/> for how those are routed.
///
/// Every entry (aliases excluded) was verified against the installed z440.atl.core
/// package via <c>AudioFormat.IsValidExtension</c> to confirm ATL genuinely recognizes it.
/// </summary>
public static class SupportedExtensions
{
    public static readonly IReadOnlySet<string> Values = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // MPEG audio
        ".mp3", ".mp2", ".mp1",
        // MPEG-4 container
        ".mp4", ".m4a", ".m4b", ".m4v",
        // AAC raw stream
        ".aac",
        // Windows Media Audio
        ".wma", ".asf",
        // Ogg container (Vorbis/Opus/Speex)
        ".ogg", ".oga", ".opus", ".spx",
        // Free Lossless Audio Codec
        ".flac",
        // Monkey's Audio
        ".ape",
        // Musepack
        ".mpc", ".mp+",
        // WavPack
        ".wv",
        // True Audio
        ".tta",
        // Tom's lossless Audio Kompressor
        ".tak",
        // OptimFROG
        ".ofr", ".ofs",
        // PCM / uncompressed
        ".wav",
        // Audio Interchange File Format
        ".aiff", ".aif", ".aifc",
        // Direct Stream Digital
        ".dsf",
        // Matroska audio/video (.webm recognized natively by ATL as Matroska,
        // same as .mka -- no alias needed; .mkv/.mk3d below are the ones that do).
        ".mka", ".webm",

        // --- Alias extensions: not natively registered by ATL, resolved via
        // ExtensionParserResolver to a canonical extension ATL does understand. ---
        ".mkv", ".mk3d", ".apl", ".flc",
    };

    public static bool IsSupported(string extension) => Values.Contains(extension);
}
