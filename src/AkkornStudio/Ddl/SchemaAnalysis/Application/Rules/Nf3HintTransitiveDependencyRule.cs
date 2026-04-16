using AkkornStudio.Ddl.SchemaAnalysis.Application.Processing;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Normalization;
using AkkornStudio.Metadata;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Rules;

public sealed class Nf3HintTransitiveDependencyRule : ISchemaAnalysisRule
{
    private readonly SchemaNameTokenizer _tokenizer = new();

    private static readonly HashSet<string> NameTokens = new(StringComparer.Ordinal)
    {
        "name",
        "nome",
    };

    private static readonly HashSet<string> AdditionalDescriptiveTokens = new(StringComparer.Ordinal)
    {
        "desc",
        "description",
        "descricao",
        "status",
    };

    public SchemaRuleCode RuleCode => SchemaRuleCode.NF3_HINT_TRANSITIVE_DEPENDENCY;

    public Task<SchemaRuleExecutionResult> ExecuteAsync(
        SchemaAnalysisExecutionContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        List<SchemaIssue> issues = [];

        foreach (TableMetadata table in context.Metadata.AllTables.OrderBy(static table => table.FullName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<ColumnDescriptor> descriptors = table.Columns
                .OrderBy(static column => column.OrdinalPosition)
                .Select(column => CreateDescriptor(column, context.Profile))
                .ToList();

            foreach (ColumnDescriptor descriptor in descriptors.Where(static item => item.IsNameColumn))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (descriptor.BaseTokens.Count == 0)
                {
                    continue;
                }

                bool hasMatchingIdColumn = descriptors.Any(candidate =>
                    candidate.IsIdColumn && BaseTokensEqual(candidate.BaseTokens, descriptor.BaseTokens)
                );
                if (!hasMatchingIdColumn)
                {
                    continue;
                }

                bool hasAdditionalDescriptiveColumn = descriptors.Any(candidate =>
                    !ReferenceEquals(candidate, descriptor)
                    && candidate.IsAdditionalDescriptiveColumn
                    && BaseTokensEqual(candidate.BaseTokens, descriptor.BaseTokens)
                );

                double score = 0.45
                    + 0.25
                    + (hasAdditionalDescriptiveColumn ? 0.15 : 0d)
                    - (IsAllowlisted(context.Profile.SemiStructuredPayloadAllowlist, table, descriptor.Column) ? 0.20 : 0d);
                score = Math.Round(Math.Clamp(score, 0d, 1d), 4, MidpointRounding.ToEven);

                double threshold = Math.Max(
                    context.Profile.MinConfidenceGlobal,
                    context.Profile.RuleSettings[SchemaRuleCode.NF3_HINT_TRANSITIVE_DEPENDENCY].MinConfidence
                );

                if (score < threshold)
                {
                    continue;
                }

                issues.Add(CreateIssue(table, descriptor.Column, descriptor.BaseTokens, score, hasAdditionalDescriptiveColumn));
            }
        }

        return Task.FromResult(new SchemaRuleExecutionResult(issues, []));
    }

    private ColumnDescriptor CreateDescriptor(ColumnMetadata column, SchemaAnalysisProfile profile)
    {
        NormalizedNameTokens tokens = _tokenizer.Tokenize(column.Name, profile);
        bool isIdColumn = tokens.StructuralTokens.Contains("id") && tokens.EntityTokens.Count > 0;
        bool isNameColumn = tokens.EntityTokens.Any(NameTokens.Contains);
        bool isAdditionalDescriptiveColumn = tokens.EntityTokens.Any(AdditionalDescriptiveTokens.Contains);

        IReadOnlyList<string> baseTokens = tokens.EntityTokens
            .Where(token => !NameTokens.Contains(token) && !AdditionalDescriptiveTokens.Contains(token))
            .ToList();

        return new ColumnDescriptor(column, baseTokens, isIdColumn, isNameColumn, isAdditionalDescriptiveColumn);
    }

    private static bool BaseTokensEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private static bool IsAllowlisted(
        IReadOnlyList<string> allowlist,
        TableMetadata table,
        ColumnMetadata column
    )
    {
        string schemaTableColumn = $"{table.Schema}.{table.Name}.{column.Name}";
        string tableColumn = $"{table.Name}.{column.Name}";
        string columnOnly = column.Name;

        if (allowlist.Any(item => string.Equals(item, schemaTableColumn, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (allowlist.Any(item => string.Equals(item, tableColumn, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return allowlist.Any(item => string.Equals(item, columnOnly, StringComparison.OrdinalIgnoreCase));
    }

    private static SchemaIssue CreateIssue(
        TableMetadata table,
        ColumnMetadata column,
        IReadOnlyList<string> baseTokens,
        double score,
        bool hasAdditionalDescriptiveColumn
    )
    {
        string entity = string.Join("_", baseTokens);
        string title = "3NF hint transitive dependency";
        string message =
            $"A coluna '{table.FullName}.{column.Name}' sugere dependência transitiva pelo padrão '{entity}_id + {column.Name}'.";

        List<SchemaEvidence> evidence =
        [
            SchemaEvidenceFactory.MetadataFact("score", score.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture), 1.0),
            SchemaEvidenceFactory.NamingMatch("canonicalEntity", entity, 0.95),
            SchemaEvidenceFactory.ConstraintTopology("pairedIdColumn", "true", 0.90),
        ];

        if (hasAdditionalDescriptiveColumn)
        {
            evidence.Add(SchemaEvidenceFactory.MetadataFact("additionalDescriptiveColumn", "true", 0.80));
        }

        return new SchemaIssue(
            IssueId: ComputeIssueId(table, column.Name, title, message, score),
            RuleCode: SchemaRuleCode.NF3_HINT_TRANSITIVE_DEPENDENCY,
            Severity: SchemaIssueSeverity.Warning,
            Confidence: score,
            TargetType: SchemaTargetType.Column,
            SchemaName: table.Schema,
            TableName: table.Name,
            ColumnName: column.Name,
            ConstraintName: null,
            Title: title,
            Message: message,
            Evidence: evidence,
            Suggestions: [],
            IsAmbiguous: false
        );
    }

    private static string ComputeIssueId(
        TableMetadata table,
        string columnName,
        string title,
        string message,
        double confidence
    )
    {
        string payload = string.Join(
            "|",
            SchemaRuleCode.NF3_HINT_TRANSITIVE_DEPENDENCY,
            SchemaTargetType.Column,
            SchemaIssueTextNormalizer.NormalizeForHash(table.Schema),
            SchemaIssueTextNormalizer.NormalizeForHash(table.Name),
            SchemaIssueTextNormalizer.NormalizeForHash(columnName),
            "∅",
            SchemaIssueTextNormalizer.NormalizeForHash(title),
            SchemaIssueTextNormalizer.NormalizeForHash(message),
            confidence.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            "0"
        );

        return SchemaIssueTextNormalizer.ComputeSha256Hex(payload);
    }

    private sealed record ColumnDescriptor(
        ColumnMetadata Column,
        IReadOnlyList<string> BaseTokens,
        bool IsIdColumn,
        bool IsNameColumn,
        bool IsAdditionalDescriptiveColumn
    );
}
