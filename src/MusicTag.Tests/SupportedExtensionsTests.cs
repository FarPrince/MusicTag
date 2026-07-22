using MusicTag.Core.Services;

namespace MusicTag.Tests;

/// <summary>
/// Locks down the exact 34-extension list from the original request/plan context section
/// ("aac/aif/aifc/aiff/ape/apl/asf/dsf/flac/flc/mka/mkv/mp+/mp1/mp2/mp3/mp4/m4a/m4b/m4v/mpc/
/// ogg/oga/ofr/ofs/opus/spx/tak/tta/wav/webm/wma/wv/mk3d") as a regression guard — added during
/// the final independent review after finding <see cref="SupportedExtensions"/> had silently
/// drifted to a *different* 34-item set (missing ".aifc"/".webm", with unrequested ".ac3"/
/// ".dff" substituted in instead) despite its own doc comment claiming "exactly the 34
/// required extensions."
/// </summary>
public class SupportedExtensionsTests
{
    private static readonly string[] RequiredExtensions =
    [
        ".aac", ".aif", ".aifc", ".aiff", ".ape", ".apl", ".asf", ".dsf", ".flac", ".flc",
        ".mka", ".mkv", ".mp+", ".mp1", ".mp2", ".mp3", ".mp4", ".m4a", ".m4b", ".m4v",
        ".mpc", ".ogg", ".oga", ".ofr", ".ofs", ".opus", ".spx", ".tak", ".tta", ".wav",
        ".webm", ".wma", ".wv", ".mk3d",
    ];

    [Fact]
    public void Values_ContainsExactlyTheRequired34Extensions()
    {
        Assert.Equal(34, RequiredExtensions.Length);
        Assert.Equal(34, SupportedExtensions.Values.Count);

        foreach (var ext in RequiredExtensions)
        {
            Assert.True(SupportedExtensions.IsSupported(ext), $"Missing required extension: {ext}");
        }
    }

    [Theory]
    [InlineData(".AIFC")]
    [InlineData(".WEBM")]
    [InlineData(".Mp3")]
    public void IsSupported_IsCaseInsensitive(string extension)
    {
        Assert.True(SupportedExtensions.IsSupported(extension));
    }
}
