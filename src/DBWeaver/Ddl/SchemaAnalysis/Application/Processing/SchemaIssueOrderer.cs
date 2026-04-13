using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Processing;

public sealed class SchemaIssueOrderer
{
    public IReadOnlyList<SchemaIssue> Order(IEnumerable<SchemaIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        return issues
            .OrderByDescending(static issue => issue.Severity)
            .ThenByDescending(static issue => issue.Confidence)
            .ThenBy(static issue => issue.RuleCode)
            .ThenBy(static issue => issue.SchemaName is null ? 1 : 0)
            .ThenBy(static issue => issue.SchemaName, StringComparer.Ordinal)
            .ThenBy(static issue => issue.TableName is null ? 1 : 0)
            .ThenBy(static issue => issue.TableName, StringComparer.Ordinal)
            .ThenBy(static issue => issue.ColumnName is null ? 1 : 0)
            .ThenBy(static issue => issue.ColumnName, StringComparer.Ordinal)
            .ThenBy(static issue => issue.ConstraintName is null ? 1 : 0)
            .ThenBy(static issue => issue.ConstraintName, StringComparer.Ordinal)
            .ThenBy(static issue => issue.IssueId, StringComparer.Ordinal)
            .ToList();
    }
}
