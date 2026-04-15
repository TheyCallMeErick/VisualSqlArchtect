using System.Globalization;
using AkkornStudio.Core;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Processing;

public static class SchemaDeterministicIdFactory
{
    public static string CreateIssueId(
        SchemaRuleCode ruleCode,
        SchemaTargetType targetType,
        string? schemaName,
        string? tableName,
        string? columnName,
        string? constraintName,
        string title,
        string message,
        double confidence,
        bool isAmbiguous
    )
    {
        string payload = string.Join(
            "|",
            ruleCode,
            targetType,
            SchemaIssueTextNormalizer.NormalizeForHash(schemaName),
            SchemaIssueTextNormalizer.NormalizeForHash(tableName),
            SchemaIssueTextNormalizer.NormalizeForHash(columnName),
            SchemaIssueTextNormalizer.NormalizeForHash(constraintName),
            SchemaIssueTextNormalizer.NormalizeForHash(title),
            SchemaIssueTextNormalizer.NormalizeForHash(message),
            Math.Round(confidence, 4, MidpointRounding.ToEven).ToString("0.0000", CultureInfo.InvariantCulture),
            isAmbiguous ? "1" : "0"
        );

        return SchemaIssueTextNormalizer.ComputeSha256Hex(payload);
    }

    public static string CreateSuggestionId(
        string issueId,
        string title,
        string description,
        double confidence
    )
    {
        string payload = string.Join(
            "|",
            SchemaIssueTextNormalizer.NormalizeForHash(issueId),
            SchemaIssueTextNormalizer.NormalizeForHash(title),
            SchemaIssueTextNormalizer.NormalizeForHash(description),
            Math.Round(confidence, 4, MidpointRounding.ToEven).ToString("0.0000", CultureInfo.InvariantCulture)
        );

        return SchemaIssueTextNormalizer.ComputeSha256Hex(payload);
    }

    public static string CreateCandidateId(
        string suggestionId,
        DatabaseProvider provider,
        string title,
        string sql,
        SqlCandidateSafety safety
    )
    {
        string payload = string.Join(
            "|",
            SchemaIssueTextNormalizer.NormalizeForHash(suggestionId),
            provider,
            SchemaIssueTextNormalizer.NormalizeForHash(title),
            SchemaIssueTextNormalizer.NormalizeForHash(sql),
            safety
        );

        return SchemaIssueTextNormalizer.ComputeSha256Hex(payload);
    }
}
