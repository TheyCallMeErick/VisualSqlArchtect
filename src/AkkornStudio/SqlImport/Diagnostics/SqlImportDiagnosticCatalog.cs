using AkkornStudio.SqlImport.Contracts;

namespace AkkornStudio.SqlImport.Diagnostics;

public static class SqlImportDiagnosticCatalog
{
    public static SqlImportDiagnostic Create(
        string code,
        SqlImportClause clause,
        string message,
        string queryId,
        string? recommendedAction = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryId);

        SqlImportDiagnosticCategory category = GetCategory(code);
        SqlImportDiagnosticSeverity severity = GetSeverity(code);
        SqlImportDiagnosticAction action = GetAction(code);

        return new SqlImportDiagnostic(
            Code: code,
            Category: category,
            Severity: severity,
            Message: message,
            Clause: clause,
            SourceSpan: null,
            SqlFragment: null,
            Action: action,
            RecommendedAction: recommendedAction ?? GetRecommendedAction(action),
            QueryId: queryId,
            CorrelationId: queryId
        );
    }

    private static SqlImportDiagnosticCategory GetCategory(string code)
    {
        return code switch
        {
            SqlImportDiagnosticCodes.ParseFatal => SqlImportDiagnosticCategory.FatalError,
            SqlImportDiagnosticCodes.AliasNormalizationLoss => SqlImportDiagnosticCategory.NormalizationLoss,
            SqlImportDiagnosticCodes.AliasNormalizationCollision => SqlImportDiagnosticCategory.Warning,
            SqlImportDiagnosticCodes.ColumnAmbiguous => SqlImportDiagnosticCategory.AmbiguityUnresolved,
            SqlImportDiagnosticCodes.ColumnUnresolved => SqlImportDiagnosticCategory.PartialImport,
            SqlImportDiagnosticCodes.AstUnsupported => SqlImportDiagnosticCategory.UnsupportedFeature,
            SqlImportDiagnosticCodes.SetOperandPrecedenceAmbiguous => SqlImportDiagnosticCategory.UnsupportedFeature,
            SqlImportDiagnosticCodes.SetOperandArityMismatch => SqlImportDiagnosticCategory.UnsupportedFeature,
            SqlImportDiagnosticCodes.SetOperandSemanticMismatch => SqlImportDiagnosticCategory.UnsupportedFeature,
            SqlImportDiagnosticCodes.FunctionGenericPreserved => SqlImportDiagnosticCategory.PartialImport,
            SqlImportDiagnosticCodes.FunctionUnsupported => SqlImportDiagnosticCategory.UnsupportedFeature,
            SqlImportDiagnosticCodes.FunctionGenericForbiddenContext => SqlImportDiagnosticCategory.FatalError,
            SqlImportDiagnosticCodes.FallbackRegexUsed => SqlImportDiagnosticCategory.FallbackActivated,
            SqlImportDiagnosticCodes.TypeInferenceFallback => SqlImportDiagnosticCategory.PartialImport,
            SqlImportDiagnosticCodes.ProjectionDroppedBlocked => SqlImportDiagnosticCategory.FatalError,
            SqlImportDiagnosticCodes.ValueMapLegacyCompat => SqlImportDiagnosticCategory.Warning,
            SqlImportDiagnosticCodes.ValueMapStructInvalid => SqlImportDiagnosticCategory.FatalError,
            SqlImportDiagnosticCodes.StarPreservedMissingMetadata => SqlImportDiagnosticCategory.UnsupportedFeature,
            SqlImportDiagnosticCodes.StarAliasUnresolved => SqlImportDiagnosticCategory.FatalError,
            SqlImportDiagnosticCodes.RoundtripNotEquivalent => SqlImportDiagnosticCategory.FatalError,
            SqlImportDiagnosticCodes.RoundtripCheckDisabled => SqlImportDiagnosticCategory.Warning,
            _ => SqlImportDiagnosticCategory.UnsupportedFeature,
        };
    }

    private static SqlImportDiagnosticSeverity GetSeverity(string code)
    {
        return code switch
        {
            SqlImportDiagnosticCodes.ParseFatal => SqlImportDiagnosticSeverity.Error,
            SqlImportDiagnosticCodes.AliasNormalizationLoss => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.AliasNormalizationCollision => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.ColumnAmbiguous => SqlImportDiagnosticSeverity.Error,
            SqlImportDiagnosticCodes.ColumnUnresolved => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.AstUnsupported => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.SetOperandPrecedenceAmbiguous => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.SetOperandArityMismatch => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.SetOperandSemanticMismatch => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.FunctionGenericPreserved => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.FunctionUnsupported => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.FunctionGenericForbiddenContext => SqlImportDiagnosticSeverity.Error,
            SqlImportDiagnosticCodes.FallbackRegexUsed => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.TypeInferenceFallback => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.ProjectionDroppedBlocked => SqlImportDiagnosticSeverity.Error,
            SqlImportDiagnosticCodes.ValueMapLegacyCompat => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.ValueMapStructInvalid => SqlImportDiagnosticSeverity.Error,
            SqlImportDiagnosticCodes.StarPreservedMissingMetadata => SqlImportDiagnosticSeverity.Warning,
            SqlImportDiagnosticCodes.StarAliasUnresolved => SqlImportDiagnosticSeverity.Error,
            SqlImportDiagnosticCodes.RoundtripNotEquivalent => SqlImportDiagnosticSeverity.Error,
            SqlImportDiagnosticCodes.RoundtripCheckDisabled => SqlImportDiagnosticSeverity.Warning,
            _ => SqlImportDiagnosticSeverity.Warning,
        };
    }

    private static SqlImportDiagnosticAction GetAction(string code)
    {
        return code switch
        {
            SqlImportDiagnosticCodes.ParseFatal => SqlImportDiagnosticAction.Abort,
            SqlImportDiagnosticCodes.FallbackRegexUsed => SqlImportDiagnosticAction.Fallback,
            SqlImportDiagnosticCodes.FunctionGenericForbiddenContext => SqlImportDiagnosticAction.Abort,
            SqlImportDiagnosticCodes.ProjectionDroppedBlocked => SqlImportDiagnosticAction.Abort,
            SqlImportDiagnosticCodes.ValueMapStructInvalid => SqlImportDiagnosticAction.Abort,
            SqlImportDiagnosticCodes.StarAliasUnresolved => SqlImportDiagnosticAction.Abort,
            SqlImportDiagnosticCodes.RoundtripNotEquivalent => SqlImportDiagnosticAction.Abort,
            SqlImportDiagnosticCodes.RoundtripCheckDisabled => SqlImportDiagnosticAction.ContinuePartial,
            _ => SqlImportDiagnosticAction.ContinuePartial,
        };
    }

    private static string GetRecommendedAction(SqlImportDiagnosticAction action)
    {
        return action switch
        {
            SqlImportDiagnosticAction.Abort => SqlImportDiagnosticMessages.AbortRecommendedAction,
            SqlImportDiagnosticAction.Fallback => SqlImportDiagnosticMessages.FallbackRecommendedAction,
            _ => SqlImportDiagnosticMessages.ContinuePartialRecommendedAction,
        };
    }
}
