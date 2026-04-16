using AkkornStudio.UI.Services.SqlImport;

namespace AkkornStudio.Tests.Unit.Services.SqlImport;

public sealed class SqlImportFeatureFlagsTests
{
    [Fact]
    public void UseAstParserAlias_UpdatesAstIrPrimary()
    {
        var flags = new SqlImportFeatureFlags();

        flags.UseAstParser = true;

        Assert.True(flags.AstIrPrimary);
        Assert.True(flags.UseAstParser);
    }

    [Fact]
    public void RegexFallbackEnabled_DefaultsToTrue()
    {
        var flags = new SqlImportFeatureFlags();

        Assert.True(flags.RegexFallbackEnabled);
    }
}
