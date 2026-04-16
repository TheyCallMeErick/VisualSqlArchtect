using AkkornStudio.SqlImport.Ids;

namespace AkkornStudio.Tests.Unit.SqlImport.Ids;

public sealed class StableSqlImportIdGeneratorTests
{
    [Fact]
    public void BuildId_WithSamePayload_ReturnsSameId()
    {
        const string payload = "Q|postgres|abc123|SqlImport.AstIrPrimary";

        string first = StableSqlImportIdGenerator.BuildId(payload);
        string second = StableSqlImportIdGenerator.BuildId(payload);

        Assert.Equal(first, second);
    }

    [Fact]
    public void BuildId_UsesExpectedShape()
    {
        string id = StableSqlImportIdGenerator.BuildId("E|q|where/0|Comparison|expr_hash");

        Assert.Equal(16, id.Length);
        Assert.Matches("^[a-z2-7]{16}$", id);
    }

    [Fact]
    public void BuildQueryId_SortsFeatureFlagsDeterministically()
    {
        string idA = StableSqlImportIdGenerator.BuildQueryId(
            "sqlserver",
            "source_hash",
            ["SqlImport.RoundTripEquivalenceCheck", "SqlImport.AstIrPrimary"]
        );

        string idB = StableSqlImportIdGenerator.BuildQueryId(
            "sqlserver",
            "source_hash",
            ["SqlImport.AstIrPrimary", "SqlImport.RoundTripEquivalenceCheck"]
        );

        Assert.Equal(idA, idB);
    }
}
