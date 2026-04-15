namespace DBWeaver.SqlImport.Diagnostics;

public static class SqlImportDiagnosticMessages
{
    public const string AbortRecommendedAction =
        "Fix fatal import diagnostics before retrying the import.";

    public const string FallbackRecommendedAction =
        "Review fallback output and migrate query shape to AST/IR-supported constructs when possible.";

    public const string ContinuePartialRecommendedAction =
        "Proceed with supported SELECT/FROM mapping and complete remaining clauses in the next milestone.";

    public const string QualifyColumnRecommendedAction =
        "Qualify the column with explicit source alias to remove ambiguity.";

    public const string SetOperandPrecedenceAmbiguous =
        "Set operation operand includes ORDER BY/LIMIT/OFFSET/TOP without parenthesization; precedence is ambiguous in AST→IR P0.";

    public const string SetOperandEmptyAfterNormalization =
        "Set operation right operand is empty after normalization in AST→IR P0.";

    public const string SetOperandParseFailed =
        "Set operation right operand could not be parsed into AST→IR P0 shape.";

    public const string SetOperandMappingFailed =
        "Set operation right operand mapping failed in AST→IR P0.";

    public const string WherePartialReportNote =
        "Some predicates were not recognized and may need manual adjustment.";

    public const string WhereUnsupportedReportNote =
        "Condition not supported by importer; connect manually after import.";

    public const string OrderByPartialReportNote =
        "Some sort terms could not be mapped and were skipped";

    public const string OrderByUnsupportedReportNote =
        "Unsupported sort expression - add manually";

    public const string GroupByPartialReportNote =
        "Some grouping terms could not be mapped and were skipped";

    public const string GroupByUnsupportedReportNote =
        "Unsupported grouping expression - add manually";

    public const string GroupByConflictReportNote =
        "Selected column is neither grouped nor aggregated";

    public const string HavingUnsupportedReportNote =
        "Complex HAVING expression - connect predicate manually";

    public const string SelectComplexExpressionManualWireReportNote =
        "Complex expression — wire manually";

    public const string SelectColumnsPartialAutoWireReportNote =
        "Some selected columns could not be auto-wired.";

    public const string CorrelatedSubqueryFallbackReportNote =
        "Correlated sub-query is not yet supported and falls back to a safe partial import path.";

    public const string CteSubqueryNotSupportedReportNote =
        "CTEs and sub-queries are not supported";

    public const string RawFallbackDisabledForCteSubqueryReportNote =
        "Raw fallback is disabled for CTE/sub-query blocks to avoid unsafe or ambiguous SQL materialization.";

    public const string UnionNotSupportedReportNote =
        "UNION is not supported";

    public const string JoinFallbackRegexReportNote =
        "JOIN ON expression used regex fallback; review ON mapping and adjust manually if needed.";

    public const string CteRewriteFallbackReportNote =
        "CTE rewrite used controlled fallback path; review rewritten shape for semantic parity.";

    public const string WhereColumnFallbackReportNote =
        "WHERE expression used fallback source-column resolution; review mapped condition if multiple sources are present.";

    public static string CorrelatedSubqueryFallbackWithExternalRefsReportNote(string correlatedFields)
    {
        return string.IsNullOrWhiteSpace(correlatedFields)
            ? CorrelatedSubqueryFallbackReportNote
            : $"{CorrelatedSubqueryFallbackReportNote} External refs: {correlatedFields}.";
    }
}
