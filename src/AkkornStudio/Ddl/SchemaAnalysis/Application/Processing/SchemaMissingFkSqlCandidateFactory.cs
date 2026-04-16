using System.Globalization;
using AkkornStudio.Core;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Processing;

public sealed class SchemaMissingFkSqlCandidateFactory
{
    private readonly SchemaForeignKeyConstraintNameGenerator _constraintNameGenerator = new();
    private readonly SchemaPreconditionsSqlGenerator _preconditionsGenerator = new();
    private readonly SchemaSqlDialectEscaping _escaping = new();

    public SqlFixCandidate? CreateCandidate(
        SchemaIssue issue,
        DatabaseProvider provider,
        string suggestionId,
        IReadOnlySet<string>? existingConstraintNames = null
    )
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestionId);

        if (issue.RuleCode != SchemaRuleCode.MISSING_FK || issue.IsAmbiguous || provider == DatabaseProvider.SQLite)
        {
            return null;
        }

        string? targetSchema = GetEvidenceValue(issue, "targetSchema") ?? issue.SchemaName;
        string? targetTable = GetEvidenceValue(issue, "targetTable");
        string? targetColumn = GetEvidenceValue(issue, "targetColumn");

        if (string.IsNullOrWhiteSpace(issue.TableName)
            || string.IsNullOrWhiteSpace(issue.ColumnName)
            || string.IsNullOrWhiteSpace(targetTable)
            || string.IsNullOrWhiteSpace(targetColumn))
        {
            return null;
        }

        string? constraintName = _constraintNameGenerator.Generate(
            issue.TableName!,
            issue.ColumnName!,
            targetTable!,
            targetColumn!,
            existingConstraintNames ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        );
        if (constraintName is null)
        {
            return null;
        }

        IReadOnlyList<string>? preconditions = _preconditionsGenerator.GenerateForeignKeyPreconditions(
            new SchemaForeignKeyPreconditionRequest(
                Provider: provider,
                SchemaName: issue.SchemaName,
                ChildTable: issue.TableName!,
                ChildColumns: [issue.ColumnName!],
                ParentTable: targetTable!,
                ParentColumns: [targetColumn!],
                ConstraintName: constraintName
            )
        );
        if (preconditions is null || preconditions.Count == 0)
        {
            return null;
        }

        string sql = BuildSql(
            provider,
            issue.SchemaName,
            issue.TableName!,
            issue.ColumnName!,
            targetSchema,
            targetTable!,
            targetColumn!,
            constraintName
        );

        const string title = "Add inferred foreign key";
        const SqlCandidateSafety safety = SqlCandidateSafety.PotentiallyDestructive;
        string candidateId = SchemaDeterministicIdFactory.CreateCandidateId(suggestionId, provider, title, sql, safety);

        return new SqlFixCandidate(
            CandidateId: candidateId,
            Provider: provider,
            Title: title,
            Sql: sql,
            PreconditionsSql: preconditions,
            Safety: safety,
            Visibility: CandidateVisibility.VisibleReadOnly,
            IsAutoApplicable: false,
            Notes:
            [
                "Este candidate altera o schema e requer revisão humana.",
                "Este candidate depende de suporte específico do provider.",
                "As preconditions devem ser avaliadas antes da execução manual.",
                "Este candidate não é autoaplicável no MVP.",
            ]
        );
    }

    private string BuildSql(
        DatabaseProvider provider,
        string? childSchema,
        string childTable,
        string childColumn,
        string? parentSchema,
        string parentTable,
        string parentColumn,
        string constraintName
    )
    {
        string childQualified = _escaping.QuoteQualifiedName(provider, childSchema, childTable);
        string parentQualified = _escaping.QuoteQualifiedName(provider, parentSchema, parentTable);
        string quotedConstraint = _escaping.QuoteIdentifier(provider, constraintName);
        string quotedChildColumn = _escaping.QuoteIdentifier(provider, childColumn);
        string quotedParentColumn = _escaping.QuoteIdentifier(provider, parentColumn);

        return $"ALTER TABLE {childQualified} ADD CONSTRAINT {quotedConstraint} FOREIGN KEY ({quotedChildColumn}) REFERENCES {parentQualified} ({quotedParentColumn});";
    }

    private static string? GetEvidenceValue(SchemaIssue issue, string key)
    {
        return issue.Evidence.FirstOrDefault(evidence => string.Equals(evidence.Key, key, StringComparison.Ordinal))?.Value;
    }
}
