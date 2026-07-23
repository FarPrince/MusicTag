using MusicTag.Core.Services;

namespace MusicTag.Tests;

public class LyricsFilenameParserTests
{
    [Theory]
    [InlineData("James Blake - When I'm Home", "James Blake", "When I'm Home")]
    [InlineData("The Beatles - Yesterday", "The Beatles", "Yesterday")]
    [InlineData("Artist - Title - With Extra Dashes", "Artist", "Title - With Extra Dashes")]
    public void TryParseArtistAndTitle_SplitsOnFirstDashSeparator(string fileName, string expectedArtist, string expectedTitle)
    {
        var parsed = LyricsFilenameParser.TryParseArtistAndTitle(fileName, out var artist, out var title);

        Assert.True(parsed);
        Assert.Equal(expectedArtist, artist);
        Assert.Equal(expectedTitle, title);
    }

    [Theory]
    [InlineData("track03")]
    [InlineData("01 Some Song")]
    [InlineData(" - Title")]
    [InlineData("Artist - ")]
    [InlineData("")]
    public void TryParseArtistAndTitle_ReturnsFalse_WhenNoUsableSeparator(string fileName)
    {
        var parsed = LyricsFilenameParser.TryParseArtistAndTitle(fileName, out var artist, out var title);

        Assert.False(parsed);
        Assert.Equal(string.Empty, artist);
        Assert.Equal(string.Empty, title);
    }
}
