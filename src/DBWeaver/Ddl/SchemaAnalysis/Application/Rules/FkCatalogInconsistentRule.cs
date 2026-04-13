using System.Security.Cryptography;
using System.Text;
using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;
using DBWeaver.Metadata;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Rules;

public sealed class FkCatalogInconsistentRule : ISchemaAnalysisRule
{
    private readonly SchemaTypeCompatibilityResolver _typeCompatibilityResolver = new();

    public SchemaRuleCode RuleCode => SchemaRuleCode.FK_CATALOG_INCONSISTENT;

    public Task<SchemaRuleExecutionResult> ExecuteAsync(
        SchemaAnalysisExecutionContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        List<SchemaIssue> issues = [];
        List<SchemaRuleExecutionDiagnostic> diagnostics = [];

        foreach (ForeignKeyRelation foreignKey in context.Metadata.AllForeignKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string childTableKey = BuildTableKey(context.Metadata.Provider, foreignKey.ChildSchema, foreignKey.ChildTable);
            string parentTableKey = BuildTableKey(context.Metadata.Provider, foreignKey.ParentSchema, foreignKey.ParentTable);

            bool childTableExists = context.Indices.TableByFullName.TryGetValue(childTableKey, out TableMetadata? childTable);
            bool parentTableExists = context.Indices.TableByFullName.TryGetValue(parentTableKey, out TableMetadata? parentTable);

            if (!childTableExists || !parentTableExists)
            {
                diagnostics.Add(CreateMetadataPartialDiagnostic());
                continue;
            }

            ColumnMetadata? childColumn = childTable!.Columns.FirstOrDefault(column =>
                string.Equals(column.Name, foreignKey.ChildColumn, StringComparison.OrdinalIgnoreCase)
            );
            if (childColumn is null)
            {
                issues.Add(CreateMissingColumnIssue(foreignKey, isChildColumn: true));
                continue;
            }

            ColumnMetadata? parentColumn = parentTable!.Columns.FirstOrDefault(column =>
                string.Equals(column.Name, foreignKey.ParentColumn, StringComparison.OrdinalIgnoreCase)
            );
            if (parentColumn is null)
            {
                issues.Add(CreateMissingColumnIssue(foreignKey, isChildColumn: false));
                continue;
            }

            SchemaTypeCompatibility compatibility = _typeCompatibilityResolver.GetCompatibility(
                childColumn.NativeType,
                parentColumn.NativeType,
                context.Metadata.Provider
            );

            if (compatibility.CompatibilityLevel is SchemaTypeCompatibilityLevel.Exact or SchemaTypeCompatibilityLevel.SemanticStrong)
            {
                continue;
            }

            issues.Add(CreateTypeCompatibilityIssue(foreignKey, childColumn, parentColumn, compatibility));
        }

        IReadOnlyList<SchemaRuleExecutionDiagnostic> orderedDiagnostics = diagnostics
            .GroupBy(static diagnostic => (diagnostic.Code, diagnostic.RuleCode))
            .Select(static group => group.First())
            .ToList();

        return Task.FromResult(new SchemaRuleExecutionResult(issues, orderedDiagnostics));
    }

    private static SchemaIssue CreateMissingColumnIssue(ForeignKeyRelation foreignKey, bool isChildColumn)
    {
        string missingSide = isChildColumn ? "child" : "parent";
        string missingColumn = isChildColumn ? foreignKey.ChildColumn : foreignKey.ParentColumn;
        string title = "FK catalog inconsistent";
        string message = $"FK '{foreignKey.ConstraintName}' referencia coluna {missingSide} inexistente: {missingColumn}.";

        List<SchemaEvidence> evidence =
        [
            new(
                EvidenceKind.MetadataFact,
                "constraintName",
                foreignKey.ConstraintName,
                1.0
            ),
            new(
                EvidenceKind.ConstraintTopology,
                $"{missingSide}ColumnMissing",
                missingColumn,
                0.95
            ),
        ];

        return new SchemaIssue(
            IssueId: ComputeIssueId(
                SchemaRuleCode.FK_CATALOG_INCONSISTENT,
                SchemaTargetType.Constraint,
                foreignKey.ChildSchema,
                foreignKey.ChildTable,
                null,
                foreignKey.ConstraintName,
                title,
                message,
                0.9500,
                false
            ),
            RuleCode: SchemaRuleCode.FK_CATALOG_INCONSISTENT,
            Severity: SchemaIssueSeverity.Critical,
            Confidence: 0.9500,
            TargetType: SchemaTargetType.Constraint,
            SchemaName: foreignKey.ChildSchema,
            TableName: foreignKey.ChildTable,
            ColumnName: null,
            ConstraintName: foreignKey.ConstraintName,
            Title: title,
            Message: message,
            Evidence: evidence,
            Suggestions: [],
            IsAmbiguous: false
        );
    }

    private static SchemaIssue CreateTypeCompatibilityIssue(
        ForeignKeyRelation foreignKey,
        ColumnMetadata childColumn,
        ColumnMetadata parentColumn,
        SchemaTypeCompatibility compatibility
    )
    {
        bool isWeak = compatibility.CompatibilityLevel == SchemaTypeCompatibilityLevel.SemanticWeak;
        string title = "FK catalog inconsistent";
        string message = isWeak
            ? $"FK '{foreignKey.ConstraintName}' usa compatibilidade semântica fraca entre '{childColumn.NativeType}' e '{parentColumn.NativeType}'."
            : $"FK '{foreignKey.ConstraintName}' usa tipos incompatíveis entre '{childColumn.NativeType}' e '{parentColumn.NativeType}'.";

        List<SchemaEvidence> evidence =
        [
            new(
                EvidenceKind.MetadataFact,
                "constraintName",
                foreignKey.ConstraintName,
                1.0
            ),
            new(
                EvidenceKind.TypeCompatibility,
                "compatibilityLevel",
                compatibility.CompatibilityLevel.ToString(),
                0.95
            ),
        ];

        return new SchemaIssue(
            IssueId: ComputeIssueId(
                SchemaRuleCode.FK_CATALOG_INCONSISTENT,
                SchemaTargetType.Constraint,
                foreignKey.ChildSchema,
                foreignKey.ChildTable,
                null,
                foreignKey.ConstraintName,
                title,
                message,
                0.9500,
                false
            ),
            RuleCode: SchemaRuleCode.FK_CATALOG_INCONSISTENT,
            Severity: isWeak ? SchemaIssueSeverity.Warning : SchemaIssueSeverity.Critical,
            Confidence: 0.9500,
            TargetType: SchemaTargetType.Constraint,
            SchemaName: foreignKey.ChildSchema,
            TableName: foreignKey.ChildTable,
            ColumnName: null,
            ConstraintName: foreignKey.ConstraintName,
            Title: title,
            Message: message,
            Evidence: evidence,
            Suggestions: [],
            IsAmbiguous: false
        );
    }

    private static SchemaRuleExecutionDiagnostic CreateMetadataPartialDiagnostic()
    {
        return new SchemaRuleExecutionDiagnostic(
            Code: "ANL-METADATA-PARTIAL",
            Message: "Metadado necessário à regra não está disponível no snapshot.",
            RuleCode: SchemaRuleCode.FK_CATALOG_INCONSISTENT,
            State: RuleExecutionState.Skipped,
            IsFatal: false
        );
    }

    private static string BuildTableKey(DatabaseProvider provider, string schemaName, string tableName)
    {
        string? canonicalSchema = SchemaCanonicalizer.Normalize(provider, schemaName);
        return string.IsNullOrWhiteSpace(canonicalSchema) ? tableName : $"{canonicalSchema}.{tableName}";
    }

    private static string ComputeIssueId(
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
            NormalizeText(schemaName),
            NormalizeText(tableName),
            NormalizeText(columnName),
            NormalizeText(constraintName),
            NormalizeText(title),
            NormalizeText(message),
            confidence.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            isAmbiguous ? "1" : "0"
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
