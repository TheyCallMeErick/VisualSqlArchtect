using DBWeaver.UI.Services.Search;

namespace DBWeaver.Tests.Unit.Services.Search;

public sealed class TextSearchServiceTests
{
    [Fact]
    public void Matches_ExactValue_ReturnsTrue()
    {
        var sut = new TextSearchService();

        bool result = sut.Matches("users", "users");

        Assert.True(result);
    }

    [Fact]
    public void Matches_FuzzySubsequence_ReturnsTrue()
    {
        var sut = new TextSearchService();

        bool result = sut.Matches("crat", "created_at");

        Assert.True(result);
    }

    [Fact]
    public void Matches_TokenizedQuery_RequiresAllTokens()
    {
        var sut = new TextSearchService();

        bool hit = sut.Matches("preview f3", "Toggle Preview", "F3");
        bool miss = sut.Matches("preview f8", "Toggle Preview", "F3");

        Assert.True(hit);
        Assert.False(miss);
    }

    [Fact]
    public void Score_BetterPrefixRanksHigherThanOnlyContains()
    {
        var sut = new TextSearchService();

        int prefix = sut.Score("tog", "toggle preview");
        int contains = sut.Score("ogg", "toggle preview");

        Assert.True(prefix > contains);
    }
}
