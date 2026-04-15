using AkkornStudio.SqlImport.Contracts;
using AkkornStudio.SqlImport.Diagnostics;

namespace AkkornStudio.Tests.Unit.SqlImport.Diagnostics;

public sealed class SqlImportDiagnosticCatalogTests
{
    [Fact]
    public void Create_WithColumnAmbiguousCode_UsesAmbiguityCategoryAndErrorSeverity()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.ColumnAmbiguous,
            SqlImportClause.Where,
            "Ambiguous column.",
            "query-1"
        );

        Assert.Equal(SqlImportDiagnosticCategory.AmbiguityUnresolved, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(SqlImportDiagnosticAction.ContinuePartial, diagnostic.Action);
        Assert.Equal(SqlImportDiagnosticMessages.ContinuePartialRecommendedAction, diagnostic.RecommendedAction);
    }

    [Fact]
    public void Create_WithColumnUnresolvedCode_UsesPartialImportCategoryAndWarningSeverity()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.ColumnUnresolved,
            SqlImportClause.OrderBy,
            "Unresolved column.",
            "query-2"
        );

        Assert.Equal(SqlImportDiagnosticCategory.PartialImport, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(SqlImportDiagnosticAction.ContinuePartial, diagnostic.Action);
    }

    [Fact]
    public void Create_WithUnknownCode_FallsBackToUnsupportedFeatureWarning()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            "SQLIMP_9999_UNKNOWN",
            SqlImportClause.Unknown,
            "Unknown behavior.",
            "query-3"
        );

        Assert.Equal(SqlImportDiagnosticCategory.UnsupportedFeature, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void Create_WithRecommendedActionOverride_UsesProvidedAction()
    {
        const string customAction = "Use explicit cast to align projection semantics.";
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.SetOperandSemanticMismatch,
            SqlImportClause.Unknown,
            "Semantic mismatch.",
            "query-4",
            customAction
        );

        Assert.Equal(customAction, diagnostic.RecommendedAction);
    }

    [Fact]
    public void Create_WithSetOperandArityMismatchCode_UsesUnsupportedFeatureWarning()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.SetOperandArityMismatch,
            SqlImportClause.Unknown,
            "Arity mismatch.",
            "query-5"
        );

        Assert.Equal(SqlImportDiagnosticCategory.UnsupportedFeature, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void Create_WithValidInput_SetsQueryAndCorrelationToSameValue()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.AstUnsupported,
            SqlImportClause.From,
            "Unsupported AST shape.",
            "query-correlation"
        );

        Assert.Equal("query-correlation", diagnostic.QueryId);
        Assert.Equal("query-correlation", diagnostic.CorrelationId);
    }

    [Fact]
    public void Create_WithParseFatalCode_UsesFatalErrorSeverityAndAbortAction()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.ParseFatal,
            SqlImportClause.Where,
            "Fatal parse failure.",
            "query-parse"
        );

        Assert.Equal(SqlImportDiagnosticCategory.FatalError, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(SqlImportDiagnosticAction.Abort, diagnostic.Action);
        Assert.Equal(SqlImportDiagnosticMessages.AbortRecommendedAction, diagnostic.RecommendedAction);
    }

    [Fact]
    public void Create_WithAliasNormalizationLoss_UsesNormalizationLossCategoryAndWarningSeverity()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.AliasNormalizationLoss,
            SqlImportClause.Select,
            "Normalization loss.",
            "query-norm-loss"
        );

        Assert.Equal(SqlImportDiagnosticCategory.NormalizationLoss, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void Create_WithAliasNormalizationCollision_UsesWarningCategoryAndWarningSeverity()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.AliasNormalizationCollision,
            SqlImportClause.Select,
            "Normalization collision.",
            "query-norm-collision"
        );

        Assert.Equal(SqlImportDiagnosticCategory.Warning, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void Create_WithFallbackRegexUsed_UsesFallbackCategoryWarningAndFallbackAction()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.FallbackRegexUsed,
            SqlImportClause.Where,
            "Regex fallback used.",
            "query-fallback"
        );

        Assert.Equal(SqlImportDiagnosticCategory.FallbackActivated, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(SqlImportDiagnosticAction.Fallback, diagnostic.Action);
        Assert.Equal(SqlImportDiagnosticMessages.FallbackRecommendedAction, diagnostic.RecommendedAction);
    }

    [Fact]
    public void Create_WithRoundtripNotEquivalent_UsesFatalErrorAndAbortAction()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.RoundtripNotEquivalent,
            SqlImportClause.Unknown,
            "Round-trip divergence detected.",
            "query-roundtrip"
        );

        Assert.Equal(SqlImportDiagnosticCategory.FatalError, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(SqlImportDiagnosticAction.Abort, diagnostic.Action);
        Assert.Equal(SqlImportDiagnosticMessages.AbortRecommendedAction, diagnostic.RecommendedAction);
    }

    [Fact]
    public void Create_WithRoundtripCheckDisabled_UsesWarningCategoryAndWarningSeverity()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.RoundtripCheckDisabled,
            SqlImportClause.Unknown,
            "Round-trip check disabled.",
            "query-roundtrip-disabled"
        );

        Assert.Equal(SqlImportDiagnosticCategory.Warning, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(SqlImportDiagnosticAction.ContinuePartial, diagnostic.Action);
    }

    [Fact]
    public void Create_WithFunctionGenericForbiddenContext_UsesFatalErrorAndAbortAction()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.FunctionGenericForbiddenContext,
            SqlImportClause.Where,
            "Generic function in forbidden context.",
            "query-fn-forbidden"
        );

        Assert.Equal(SqlImportDiagnosticCategory.FatalError, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(SqlImportDiagnosticAction.Abort, diagnostic.Action);
    }

    [Fact]
    public void Create_WithFunctionGenericPreserved_UsesPartialImportWarning()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.FunctionGenericPreserved,
            SqlImportClause.Select,
            "Generic function preserved in projection.",
            "query-fn-preserved"
        );

        Assert.Equal(SqlImportDiagnosticCategory.PartialImport, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(SqlImportDiagnosticAction.ContinuePartial, diagnostic.Action);
    }

    [Fact]
    public void Create_WithStarAliasUnresolved_UsesFatalErrorAndAbortAction()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.StarAliasUnresolved,
            SqlImportClause.Star,
            "Star alias unresolved.",
            "query-star-alias"
        );

        Assert.Equal(SqlImportDiagnosticCategory.FatalError, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(SqlImportDiagnosticAction.Abort, diagnostic.Action);
    }

    [Fact]
    public void Create_WithValueMapLegacyCompat_UsesWarningCategoryAndWarningSeverity()
    {
        SqlImportDiagnostic diagnostic = SqlImportDiagnosticCatalog.Create(
            SqlImportDiagnosticCodes.ValueMapLegacyCompat,
            SqlImportClause.ValueMap,
            "Legacy ValueMap compatibility mode.",
            "query-valuemap-legacy"
        );

        Assert.Equal(SqlImportDiagnosticCategory.Warning, diagnostic.Category);
        Assert.Equal(SqlImportDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(SqlImportDiagnosticAction.ContinuePartial, diagnostic.Action);
    }
}
