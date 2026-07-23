using MusicTag.Core.Models;

namespace MusicTag.Tests;

public class SeparatorNormalizationTests
{
    [Theory]
    [InlineData("Rock;Pop", "Rock, Pop")]
    [InlineData("Rock; Pop", "Rock, Pop")]
    [InlineData("Rock ;Pop", "Rock, Pop")]
    [InlineData("Rock;Pop;Jazz", "Rock, Pop, Jazz")]
    [InlineData("Rock;;Pop", "Rock, Pop")]
    [InlineData(";Rock;", "Rock")]
    public void Normalize_ReplacesSemicolonsAndTrimsWhitespace(string input, string expected)
    {
        Assert.Equal(expected, SeparatorNormalization.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Rock")]
    [InlineData("Rock, Pop")]
    public void Normalize_LeavesValuesWithoutSemicolonsUnchanged(string? input)
    {
        Assert.Equal(input, SeparatorNormalization.Normalize(input));
    }

    [Fact]
    public void Normalize_AllSemicolonsAndWhitespace_ReturnsNull()
    {
        Assert.Null(SeparatorNormalization.Normalize(" ; ; "));
    }

    [Fact]
    public void Apply_OnlyTouchesEnabledFields()
    {
        var fields = new TagFieldSet { Artist = "A;B", Genre = "X;Y", Title = "T;U" };
        var enabled = new HashSet<string> { "Artist", "Genre" };

        var result = SeparatorNormalization.Apply(fields, enabled);

        Assert.Equal("A, B", result.Artist);
        Assert.Equal("X, Y", result.Genre);
        Assert.Equal("T;U", result.Title); // Not enabled — left untouched.
    }

    [Fact]
    public void Apply_NoFieldsEnabled_ReturnsEquivalentFieldSet()
    {
        var fields = new TagFieldSet { Artist = "A;B" };

        var result = SeparatorNormalization.Apply(fields, new HashSet<string>());

        Assert.Equal(fields, result);
    }
}
