using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaSuggestionFactoryTests
{
    private readonly SchemaSuggestionFactory _factory = new();

    [Fact]
    public void CreateSuggestions_ReturnsSingleSuggestion_ForSupportedIssue()
    {
        SchemaIssue issue = SchemaAnalysisContractValidatorTestData.CreateIssue(
            issueId: "issue-comment",
            targetType: SchemaTargetType.Table,
            tableName: "orders",
            columnName: null,
            suggestions: []
        ) with
        {
            RuleCode = SchemaRuleCode.MISSING_REQUIRED_COMMENT,
            Title = "Missing required comment",
            Message = "Comment required"
        };

        IReadOnlyList<SchemaSuggestion> suggestions = _factory.CreateSuggestions(
            issue,
            DatabaseProvider.Postgres,
            SchemaAnalysisContractValidatorTestData.CreateProfile()
        );

        SchemaSuggestion suggestion = Assert.Single(suggestions);
        Assert.Equal("Add technical comment", suggestion.Title);
        Assert.Empty(suggestion.SqlCandidates);
    }

    [Fact]
    public void CreateSuggestions_Truncates_ByConfidenceThenTitleThenSuggestionId()
    {
        SchemaIssue issue = SchemaAnalysisContractValidatorTestData.CreateIssue(
            issueId: "issue-fk",
            suggestions: []
        ) with
        {
            RuleCode = SchemaRuleCode.MISSING_FK,
            Title = "Missing FK",
            Message = "FK inferred"
        };
        SchemaAnalysisProfile profile = SchemaAnalysisContractValidatorTestData.CreateProfile(maxSuggestionsPerIssue: 1);

        IReadOnlyList<SchemaSuggestion> suggestions = _factory.CreateSuggestions(issue, DatabaseProvider.Postgres, profile);

        SchemaSuggestion suggestion = Assert.Single(suggestions);
        Assert.Equal("Review inferred foreign key", suggestion.Title);
    }

    [Fact]
    public void CreateSuggestions_ReturnsEmpty_WhenIssueHasNoSuggestionMapping()
    {
        SchemaIssue issue = SchemaAnalysisContractValidatorTestData.CreateIssue(
            issueId: "issue-fkcat",
            targetType: SchemaTargetType.Constraint,
            constraintName: "fk_orders_customer",
            suggestions: []
        ) with
        {
            RuleCode = SchemaRuleCode.FK_CATALOG_INCONSISTENT,
            Title = "FK catalog inconsistent",
            Message = "Constraint inconsistent"
        };

        IReadOnlyList<SchemaSuggestion> suggestions = _factory.CreateSuggestions(
            issue,
            DatabaseProvider.Postgres,
            SchemaAnalysisContractValidatorTestData.CreateProfile()
        );

        Assert.Empty(suggestions);
    }
}
