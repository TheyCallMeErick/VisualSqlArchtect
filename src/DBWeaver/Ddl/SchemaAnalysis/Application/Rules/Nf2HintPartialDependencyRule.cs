using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;
using DBWeaver.Metadata;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Rules;

public sealed class Nf2HintPartialDependencyRule : ISchemaAnalysisRule
{
    private readonly SchemaNameTokenizer _tokenizer = new();

    private static readonly HashSet<string> DescriptiveTokens = new(StringComparer.Ordinal)
    {
        "name",
        "nome",
        "description",
        "descricao",
        "desc",
        "status",
        "label",
        "title",
        "titulo",
    };

    public SchemaRuleCode RuleCode => SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY;

    public Task<SchemaRuleExecutionResult> ExecuteAsync(
        SchemaAnalysisExecutionContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        List<SchemaIssue> issues = [];

        foreach ((string tableKey, IReadOnlyList<ColumnMetadata> pkColumns) in context.Indices.PkColumnsByTable)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pkColumns.Count < 2)
            {
                continue;
            }

            TableMetadata table = context.Indices.TableByFullName[tableKey];
            IReadOnlyDictionary<string, NormalizedNameTokens> pkTokenMap = pkColumns.ToDictionary(
                static column => column.Name,
                column => _tokenizer.Tokenize(column.Name, context.Profile),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (ColumnMetadata candidateColumn in table.Columns
                         .Where(static column => !column.IsPrimaryKey)
                         .OrderBy(static column => column.OrdinalPosition))
            {
                cancellationToken.ThrowIfCancellationRequested();

                NormalizedNameTokens candidateTokens = _tokenizer.Tokenize(candidateColumn.Name, context.Profile);
                if (!candidateTokens.HasEntityTokens)
                {
                    continue;
                }

                AssociationResult strongAssociation = FindStrongAssociation(
                    candidateTokens.EntityTokens,
                    pkColumns,
                    pkTokenMap
                );

                AssociationResult descriptiveAssociation = FindStrongAssociation(
                    candidateTokens.EntityTokens.Where(token => !DescriptiveTokens.Contains(token)).ToList(),
                    pkColumns,
                    pkTokenMap
                );

                bool descriptiveColumnLinked =
                    candidateTokens.EntityTokens.Any(token => DescriptiveTokens.Contains(token))
                    && descriptiveAssociation.HasAssociation
                    && strongAssociation.HasAssociation
                    && string.Equals(
                        strongAssociation.ComponentName,
                        descriptiveAssociation.ComponentName,
                        StringComparison.OrdinalIgnoreCase
                    );

                int objectiveSignals = CountTrue(strongAssociation.HasAssociation, descriptiveColumnLinked);
                if (objectiveSignals < 2)
                {
                    continue;
                }

                bool participatesInUniqueIndexWithFullPk = ParticipatesInUniqueIndexWithFullPk(
                    table,
                    candidateColumn.Name,
                    pkColumns
                );

                double score = 0.35
                    + (strongAssociation.HasAssociation ? 0.30 : 0d)
                    + (descriptiveColumnLinked ? 0.20 : 0d)
                    - (participatesInUniqueIndexWithFullPk ? 0.25 : 0d);
                score = Math.Round(Math.Clamp(score, 0d, 1d), 4, MidpointRounding.ToEven);

                double threshold = Math.Max(
                    context.Profile.MinConfidenceGlobal,
                    context.Profile.RuleSettings[SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY].MinConfidence
                );

                if (score < threshold)
                {
                    continue;
                }

                bool? dimensionCriterionMatched = EvaluateDimensionCriterion(
                    context,
                    table,
                    strongAssociation.ComponentName!
                );

                issues.Add(
                    CreateIssue(
                        table,
                        candidateColumn,
                        strongAssociation.ComponentName!,
                        score,
                        participatesInUniqueIndexWithFullPk,
                        dimensionCriterionMatched
                    )
                );
            }
        }

        return Task.FromResult(new SchemaRuleExecutionResult(issues, []));
    }

    private static AssociationResult FindStrongAssociation(
        IReadOnlyList<string> candidateEntityTokens,
        IReadOnlyList<ColumnMetadata> pkColumns,
        IReadOnlyDictionary<string, NormalizedNameTokens> pkTokenMap
    )
    {
        if (candidateEntityTokens.Count == 0)
        {
            return AssociationResult.None;
        }

        List<string> matchingComponents = [];

        foreach (ColumnMetadata pkColumn in pkColumns)
        {
            IReadOnlySet<string> pkTokens = pkTokenMap[pkColumn.Name].EntityTokens.ToHashSet(StringComparer.Ordinal);
            bool intersects = candidateEntityTokens.Any(pkTokens.Contains);
            if (intersects)
            {
                matchingComponents.Add(pkColumn.Name);
            }
        }

        return matchingComponents.Count == 1
            ? new AssociationResult(true, matchingComponents[0])
            : AssociationResult.None;
    }

    private static bool ParticipatesInUniqueIndexWithFullPk(
        TableMetadata table,
        string candidateColumnName,
        IReadOnlyList<ColumnMetadata> pkColumns
    )
    {
        HashSet<string> requiredColumns = pkColumns
            .Select(static column => column.Name)
            .Append(candidateColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return table.Indexes.Any(index =>
            index.IsUnique
            && !index.IsPrimaryKey
            && requiredColumns.IsSubsetOf(index.Columns.ToHashSet(StringComparer.OrdinalIgnoreCase))
        );
    }

    private static bool? EvaluateDimensionCriterion(
        SchemaAnalysisExecutionContext context,
        TableMetadata sourceTable,
        string associatedPkColumnName
    )
    {
        ForeignKeyRelation? foreignKey = sourceTable.OutboundForeignKeys.FirstOrDefault(foreignKey =>
            string.Equals(foreignKey.ChildColumn, associatedPkColumnName, StringComparison.OrdinalIgnoreCase)
        );

        if (foreignKey is null)
        {
            return false;
        }

        string parentKey = BuildTableKey(context.Metadata.Provider, foreignKey.ParentSchema, foreignKey.ParentTable);
        if (!context.Indices.TableByFullName.TryGetValue(parentKey, out TableMetadata? parentTable))
        {
            return false;
        }

        if (!context.Indices.PkColumnsByTable.TryGetValue(parentKey, out IReadOnlyList<ColumnMetadata>? parentPkColumns)
            || parentPkColumns.Count != 1)
        {
            return false;
        }

        if (sourceTable.EstimatedRowCount is null || parentTable.EstimatedRowCount is null)
        {
            return null;
        }

        return parentTable.EstimatedRowCount.Value < sourceTable.EstimatedRowCount.Value;
    }

    private static SchemaIssue CreateIssue(
        TableMetadata table,
        ColumnMetadata candidateColumn,
        string associatedPkComponent,
        double score,
        bool participatesInUniqueIndexWithFullPk,
        bool? dimensionCriterionMatched
    )
    {
        string title = "2NF hint partial dependency";
        string message =
            $"A coluna '{table.FullName}.{candidateColumn.Name}' sugere dependÃªncia parcial do componente '{associatedPkComponent}' da PK composta.";

        List<SchemaEvidence> evidence =
        [
            SchemaEvidenceFactory.MetadataFact("score", score.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture), 1.0),
            SchemaEvidenceFactory.NamingMatch("associatedPkComponent", associatedPkComponent, 0.95),
            SchemaEvidenceFactory.ConstraintTopology("compositePrimaryKey", "true", 0.90),
        ];

        if (participatesInUniqueIndexWithFullPk)
        {
            evidence.Add(SchemaEvidenceFactory.ConstraintTopology("uniqueIndexWithFullPk", "true", 0.75));
        }

        if (dimensionCriterionMatched.HasValue)
        {
            evidence.Add(
                SchemaEvidenceFactory.MetadataFact(
                    "dimensionCriterionMatched",
                    dimensionCriterionMatched.Value ? "true" : "false",
                    0.70
                )
            );
        }

        return new SchemaIssue(
            IssueId: ComputeIssueId(table, candidateColumn.Name, title, message, score),
            RuleCode: SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY,
            Severity: SchemaIssueSeverity.Warning,
            Confidence: score,
            TargetType: SchemaTargetType.Column,
            SchemaName: table.Schema,
            TableName: table.Name,
            ColumnName: candidateColumn.Name,
            ConstraintName: null,
            Title: title,
            Message: message,
            Evidence: evidence,
            Suggestions: [],
            IsAmbiguous: false
        );
    }

    private static int CountTrue(params bool[] values) => values.Count(static value => value);

    private static string BuildTableKey(DatabaseProvider provider, string schemaName, string tableName)
    {
        string? canonicalSchema = SchemaCanonicalizer.Normalize(provider, schemaName);
        return string.IsNullOrWhiteSpace(canonicalSchema) ? tableName : $"{canonicalSchema}.{tableName}";
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
            SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY,
            SchemaTargetType.Column,
            SchemaIssueTextNormalizer.NormalizeForHash(table.Schema),
            SchemaIssueTextNormalizer.NormalizeForHash(table.Name),
            SchemaIssueTextNormalizer.NormalizeForHash(columnName),
            "âˆ…",
            SchemaIssueTextNormalizer.NormalizeForHash(title),
            SchemaIssueTextNormalizer.NormalizeForHash(message),
            confidence.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            "0"
        );

        return SchemaIssueTextNormalizer.ComputeSha256Hex(payload);
    }

    private sealed record AssociationResult(bool HasAssociation, string? ComponentName)
    {
        public static AssociationResult None { get; } = new(false, null);
    }
}
