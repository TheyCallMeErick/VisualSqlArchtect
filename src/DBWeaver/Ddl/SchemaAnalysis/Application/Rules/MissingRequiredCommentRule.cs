using System.Security.Cryptography;
using System.Text;
using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Application.Indexing;
using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Metadata;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Rules;

public sealed class MissingRequiredCommentRule : ISchemaAnalysisRule
{
    private static readonly HashSet<string> AuditColumnNames = new(StringComparer.Ordinal)
    {
        "created_at",
        "updated_at",
        "deleted_at",
        "created_by",
        "updated_by",
        "deleted_by",
    };

    public SchemaRuleCode RuleCode => SchemaRuleCode.MISSING_REQUIRED_COMMENT;

    public Task<SchemaRuleExecutionResult> ExecuteAsync(
        SchemaAnalysisExecutionContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Profile.RequiredCommentTargets.Count == 0)
        {
            return Task.FromResult(new SchemaRuleExecutionResult([], []));
        }

        HashSet<string> requiredTargets = new(
            context.Profile.RequiredCommentTargets,
            StringComparer.OrdinalIgnoreCase
        );
        List<SchemaIssue> issues = [];

        foreach ((string tableKey, TableMetadata table) in context.Indices.TableByFullName.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (requiredTargets.Contains("Table") && !HasValidComment(table.Comment, table.Name))
            {
                issues.Add(CreateTableIssue(context.Metadata.Provider, table, "Table"));
            }

            HashSet<string> fkColumns = table.OutboundForeignKeys
                .Select(static foreignKey => foreignKey.ChildColumn)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            HashSet<string> uniqueColumns = context.Indices.UniqueConstraintsByTable.TryGetValue(tableKey, out IReadOnlyList<SchemaUniqueConstraintDescriptor>? uniqueConstraints)
                ? uniqueConstraints
                    .Where(static uniqueConstraint => uniqueConstraint.Columns.Count == 1)
                    .Select(static uniqueConstraint => uniqueConstraint.Columns[0])
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ColumnMetadata column in table.Columns.OrderBy(static column => column.OrdinalPosition))
            {
                string targetKind = ResolveColumnTargetKind(requiredTargets, table, column, fkColumns, uniqueColumns);
                if (targetKind.Length == 0)
                {
                    continue;
                }

                if (HasValidComment(column.Comment, column.Name))
                {
                    continue;
                }

                issues.Add(CreateColumnIssue(context.Metadata.Provider, table, column, targetKind));
            }
        }

        return Task.FromResult(new SchemaRuleExecutionResult(issues, []));
    }

    private static string ResolveColumnTargetKind(
        IReadOnlySet<string> requiredTargets,
        TableMetadata table,
        ColumnMetadata column,
        IReadOnlySet<string> fkColumns,
        IReadOnlySet<string> uniqueColumns
    )
    {
        if (requiredTargets.Contains("PrimaryKeyColumn") && column.IsPrimaryKey)
        {
            return "PrimaryKeyColumn";
        }

        if (requiredTargets.Contains("ForeignKeyColumn") && fkColumns.Contains(column.Name))
        {
            return "ForeignKeyColumn";
        }

        if (requiredTargets.Contains("UniqueColumn") && uniqueColumns.Contains(column.Name))
        {
            return "UniqueColumn";
        }

        if (requiredTargets.Contains("AuditColumn"))
        {
            string normalizedName = string.Join("_", table.Columns
                .Where(existing => string.Equals(existing.Name, column.Name, StringComparison.OrdinalIgnoreCase))
                .Select(static existing => existing.Name)
                .Take(1));

            normalizedName = normalizedName.Length == 0 ? column.Name : normalizedName;
            normalizedName = SchemaIssueTextNormalizer.NormalizeForHash(normalizedName).Replace(' ', '_');

            if (AuditColumnNames.Contains(normalizedName))
            {
                return "AuditColumn";
            }
        }

        return string.Empty;
    }

    private static bool HasValidComment(string? comment, string objectName)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return false;
        }

        string normalizedComment = SchemaIssueTextNormalizer.NormalizeForHash(comment);
        string normalizedObjectName = SchemaIssueTextNormalizer.NormalizeForHash(objectName);
        return !string.Equals(normalizedComment, normalizedObjectName, StringComparison.Ordinal);
    }

    private static SchemaIssue CreateTableIssue(
        DatabaseProvider provider,
        TableMetadata table,
        string targetKind
    )
    {
        string title = "Missing required comment";
        string message = $"A tabela '{table.FullName}' exige comentário válido para '{targetKind}'.";

        return new SchemaIssue(
            IssueId: ComputeIssueId(table.Schema, table.Name, null, title, message, GetConfidence(provider)),
            RuleCode: SchemaRuleCode.MISSING_REQUIRED_COMMENT,
            Severity: GetSeverity(provider),
            Confidence: GetConfidence(provider),
            TargetType: SchemaTargetType.Table,
            SchemaName: table.Schema,
            TableName: table.Name,
            ColumnName: null,
            ConstraintName: null,
            Title: title,
            Message: message,
            Evidence: BuildEvidence(provider, targetKind),
            Suggestions: [],
            IsAmbiguous: false
        );
    }

    private static SchemaIssue CreateColumnIssue(
        DatabaseProvider provider,
        TableMetadata table,
        ColumnMetadata column,
        string targetKind
    )
    {
        string title = "Missing required comment";
        string message = $"A coluna '{table.FullName}.{column.Name}' exige comentário válido para '{targetKind}'.";

        return new SchemaIssue(
            IssueId: ComputeIssueId(table.Schema, table.Name, column.Name, title, message, GetConfidence(provider)),
            RuleCode: SchemaRuleCode.MISSING_REQUIRED_COMMENT,
            Severity: GetSeverity(provider),
            Confidence: GetConfidence(provider),
            TargetType: SchemaTargetType.Column,
            SchemaName: table.Schema,
            TableName: table.Name,
            ColumnName: column.Name,
            ConstraintName: null,
            Title: title,
            Message: message,
            Evidence: BuildEvidence(provider, targetKind),
            Suggestions: [],
            IsAmbiguous: false
        );
    }

    private static IReadOnlyList<SchemaEvidence> BuildEvidence(DatabaseProvider provider, string targetKind)
    {
        List<SchemaEvidence> evidence =
        [
            new(
                EvidenceKind.PolicyRequirement,
                "requiredCommentTarget",
                targetKind,
                1.0
            ),
        ];

        if (provider == DatabaseProvider.SQLite)
        {
            evidence.Add(
                new SchemaEvidence(
                    EvidenceKind.ProviderLimitation,
                    "provider",
                    provider.ToString(),
                    0.9
                )
            );
        }
        else
        {
            evidence.Add(
                new SchemaEvidence(
                    EvidenceKind.MetadataFact,
                    "commentState",
                    "MissingOrEquivalent",
                    0.9
                )
            );
        }

        return evidence;
    }

    private static SchemaIssueSeverity GetSeverity(DatabaseProvider provider)
    {
        return provider == DatabaseProvider.SQLite
            ? SchemaIssueSeverity.Info
            : SchemaIssueSeverity.Warning;
    }

    private static double GetConfidence(DatabaseProvider provider)
    {
        return provider == DatabaseProvider.SQLite ? 0.6000 : 0.8500;
    }

    private static string ComputeIssueId(
        string? schemaName,
        string? tableName,
        string? columnName,
        string title,
        string message,
        double confidence
    )
    {
        string payload = string.Join(
            "|",
            SchemaRuleCode.MISSING_REQUIRED_COMMENT,
            columnName is null ? SchemaTargetType.Table : SchemaTargetType.Column,
            NormalizeText(schemaName),
            NormalizeText(tableName),
            NormalizeText(columnName),
            "∅",
            NormalizeText(title),
            NormalizeText(message),
            confidence.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            "0"
        );

        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "∅";
        }

        string trimmed = value.Normalize(NormalizationForm.FormC).Trim();
        string collapsed = string.Join(" ", trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.ToLowerInvariant();
    }
}
