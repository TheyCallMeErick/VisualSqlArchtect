using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Processing;

public sealed class SchemaIssueDeduplicator
{
    public IReadOnlyList<SchemaIssue> Deduplicate(IEnumerable<SchemaIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        return issues
            .GroupBy(BuildIssueDedupeKey, StringComparer.Ordinal)
            .Select(static group => group
                .OrderByDescending(static issue => issue.Confidence)
                .ThenByDescending(static issue => issue.Severity)
                .ThenBy(static issue => issue.IssueId, StringComparer.Ordinal)
                .First())
            .ToList();
    }

    private static string BuildIssueDedupeKey(SchemaIssue issue)
    {
        string messageHash = SchemaIssueTextNormalizer.ComputeSha256Hex(
            SchemaIssueTextNormalizer.NormalizeForHash(issue.Message)
        );

        return string.Join(
            "|",
            issue.RuleCode,
            issue.TargetType,
            issue.SchemaName ?? string.Empty,
            issue.TableName ?? string.Empty,
            issue.ColumnName ?? string.Empty,
            issue.ConstraintName ?? string.Empty,
            messageHash
        );
    }
}
