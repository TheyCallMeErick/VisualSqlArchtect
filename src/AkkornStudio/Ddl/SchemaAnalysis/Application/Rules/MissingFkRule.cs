using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AkkornStudio.Core;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Indexing;
using AkkornStudio.Ddl.SchemaAnalysis.Application.Processing;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Normalization;
using AkkornStudio.Metadata;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Rules;

public sealed partial class MissingFkRule : ISchemaAnalysisRule
{
    private readonly SchemaNameTokenizer _tokenizer = new();
    private readonly SchemaTokenEquivalenceResolver _equivalenceResolver = new();
    private readonly SchemaTypeCompatibilityResolver _typeCompatibilityResolver = new();

    public SchemaRuleCode RuleCode => SchemaRuleCode.MISSING_FK;

    public Task<SchemaRuleExecutionResult> ExecuteAsync(
        SchemaAnalysisExecutionContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        List<SchemaIssue> issues = [];
        SchemaTokenEquivalenceResolution equivalence = _equivalenceResolver.Resolve(context.Profile);

        foreach (KeyValuePair<string, IReadOnlyList<ColumnMetadata>> entry in context.Indices.ColumnsByTable)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string tableKey = entry.Key;
            TableMetadata table = context.Indices.TableByFullName[tableKey];

            foreach (ColumnMetadata column in entry.Value)
            {
                if (!IsEligibleSourceColumn(column))
                {
                    continue;
                }

                NormalizedNameTokens canonicalTokens = _tokenizer.Tokenize(column.Name, context.Profile);
                if (!canonicalTokens.HasEntityTokens)
                {
                    continue;
                }

                NormalizedNameTokens rawTokens = _tokenizer.Tokenize(column.Name);
                string sourcePrincipalCanonical = canonicalTokens.PrincipalEntityToken!;
                string? sourcePrincipalRaw = rawTokens.PrincipalEntityToken;

                bool genericName = IsGenericName(sourcePrincipalCanonical, equivalence);
                bool indexedSource = column.IsIndexed || IsIndexedByMetadata(table, column.Name);

                List<CandidateTarget> candidates = BuildCandidateTargets(
                    context,
                    table,
                    column,
                    sourcePrincipalCanonical,
                    sourcePrincipalRaw,
                    indexedSource,
                    genericName
                );

                if (candidates.Count == 0)
                {
                    continue;
                }

                bool ambiguousTargets = candidates.Count > 1;
                List<CandidateTarget> rescoredCandidates = candidates
                    .Select(candidate => candidate with
                    {
                        Score = RoundScore(ComputeScore(candidate, indexedSource, ambiguousTargets, genericName)),
                    })
                    .Where(candidate => candidate.Score > 0)
                    .OrderByDescending(static candidate => candidate.Score)
                    .ThenByDescending(static candidate => candidate.ExactEntityMatch)
                    .ThenByDescending(static candidate => candidate.TargetIsPk)
                    .ThenBy(static candidate => candidate.TargetTableFullName, StringComparer.Ordinal)
                    .ToList();

                if (rescoredCandidates.Count == 0)
                {
                    continue;
                }

                CandidateTarget best = rescoredCandidates[0];
                double threshold = Math.Max(
                    context.Profile.MinConfidenceGlobal,
                    context.Profile.RuleSettings[SchemaRuleCode.MISSING_FK].MinConfidence
                );

                if (best.Score < threshold)
                {
                    continue;
                }

                bool isAmbiguous = IsAmbiguous(rescoredCandidates);
                issues.Add(CreateIssue(table, column, best, best.Score, isAmbiguous, rescoredCandidates.Take(2).ToList()));
            }
        }

        return Task.FromResult(new SchemaRuleExecutionResult(issues, []));
    }

    private List<CandidateTarget> BuildCandidateTargets(
        SchemaAnalysisExecutionContext context,
        TableMetadata sourceTable,
        ColumnMetadata sourceColumn,
        string sourcePrincipalCanonical,
        string? sourcePrincipalRaw,
        bool indexedSource,
        bool genericName
    )
    {
        List<CandidateTarget> candidates = [];

        foreach (KeyValuePair<string, TableMetadata> tableEntry in context.Indices.TableByFullName)
        {
            TableMetadata targetTable = tableEntry.Value;
            string targetTableFullName = tableEntry.Key;

            if (string.Equals(sourceTable.FullName, targetTable.FullName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            NormalizedNameTokens targetCanonicalTokens = _tokenizer.Tokenize(targetTable.Name, context.Profile);
            NormalizedNameTokens targetRawTokens = _tokenizer.Tokenize(targetTable.Name);

            bool exactEntityMatch =
                sourcePrincipalRaw is not null
                && targetRawTokens.PrincipalEntityToken is not null
                && string.Equals(sourcePrincipalRaw, targetRawTokens.PrincipalEntityToken, StringComparison.Ordinal);
            bool synonymEntityMatch =
                targetCanonicalTokens.PrincipalEntityToken is not null
                && string.Equals(sourcePrincipalCanonical, targetCanonicalTokens.PrincipalEntityToken, StringComparison.Ordinal);

            if (!exactEntityMatch && !synonymEntityMatch)
            {
                continue;
            }

            if (context.Indices.PkColumnsByTable.TryGetValue(targetTableFullName, out IReadOnlyList<ColumnMetadata>? pkColumns)
                && pkColumns.Count == 1)
            {
                AddCandidateIfCompatible(
                    context,
                    sourceTable,
                    sourceColumn,
                    targetTable,
                    targetTableFullName,
                    pkColumns[0],
                    exactEntityMatch,
                    synonymEntityMatch,
                    targetIsPk: true,
                    targetIsUq: false,
                    indexedSource,
                    genericName,
                    candidates
                );
            }

            if (context.Indices.UniqueConstraintsByTable.TryGetValue(targetTableFullName, out IReadOnlyList<SchemaUniqueConstraintDescriptor>? uniqueConstraints))
            {
                foreach (SchemaUniqueConstraintDescriptor uniqueConstraint in uniqueConstraints.Where(static uq => uq.Columns.Count == 1))
                {
                    ColumnMetadata? targetColumn = targetTable.Columns.FirstOrDefault(column =>
                        string.Equals(column.Name, uniqueConstraint.Columns[0], StringComparison.OrdinalIgnoreCase)
                    );

                    if (targetColumn is null)
                    {
                        continue;
                    }

                    AddCandidateIfCompatible(
                        context,
                        sourceTable,
                        sourceColumn,
                        targetTable,
                        targetTableFullName,
                        targetColumn,
                        exactEntityMatch,
                        synonymEntityMatch,
                        targetIsPk: false,
                        targetIsUq: true,
                        indexedSource,
                        genericName,
                        candidates
                    );
                }
            }
        }

        return candidates;
    }

    private void AddCandidateIfCompatible(
        SchemaAnalysisExecutionContext context,
        TableMetadata sourceTable,
        ColumnMetadata sourceColumn,
        TableMetadata targetTable,
        string targetTableFullName,
        ColumnMetadata targetColumn,
        bool exactEntityMatch,
        bool synonymEntityMatch,
        bool targetIsPk,
        bool targetIsUq,
        bool indexedSource,
        bool genericName,
        ICollection<CandidateTarget> candidates
    )
    {
        SchemaTypeCompatibility compatibility = _typeCompatibilityResolver.GetCompatibility(
            sourceColumn.NativeType,
            targetColumn.NativeType,
            context.Metadata.Provider
        );

        if (compatibility.CompatibilityLevel == SchemaTypeCompatibilityLevel.Exact
            || compatibility.CompatibilityLevel == SchemaTypeCompatibilityLevel.SemanticStrong)
        {
            candidates.Add(
                new CandidateTarget(
                    TargetTableFullName: targetTableFullName,
                    TargetTable: targetTable,
                    TargetColumn: targetColumn,
                    ExactEntityMatch: exactEntityMatch,
                    SynonymEntityMatch: synonymEntityMatch,
                    IdPattern: _tokenizer.Tokenize(sourceColumn.Name, context.Profile).StructuralTokens.Contains("id"),
                    TypeExact: compatibility.CompatibilityLevel == SchemaTypeCompatibilityLevel.Exact,
                    TypeSemanticStrong: compatibility.CompatibilityLevel == SchemaTypeCompatibilityLevel.SemanticStrong,
                    TargetIsPk: targetIsPk,
                    TargetIsUq: targetIsUq,
                    IndexedSource: indexedSource,
                    GenericName: genericName,
                    Score: 0d
                )
            );
        }
    }

    private static bool IsEligibleSourceColumn(ColumnMetadata column)
    {
        return !column.IsPrimaryKey && !column.IsForeignKey;
    }

    private static bool IsIndexedByMetadata(TableMetadata table, string columnName)
    {
        return table.Indexes.Any(index =>
            index.Columns.Any(indexColumn => string.Equals(indexColumn, columnName, StringComparison.OrdinalIgnoreCase))
        );
    }

    private static bool IsGenericName(string principalToken, SchemaTokenEquivalenceResolution equivalence)
    {
        if (equivalence.IsAllowlisted(principalToken))
        {
            return false;
        }

        if (equivalence.IsDenylisted(principalToken))
        {
            return true;
        }

        return GenericNamePattern().IsMatch(principalToken);
    }

    private static double ComputeScore(
        CandidateTarget candidate,
        bool indexedSource,
        bool ambiguousTargets,
        bool genericName
    )
    {
        double score =
            0.30 * ToDouble(candidate.ExactEntityMatch)
            + 0.20 * ToDouble(candidate.SynonymEntityMatch)
            + 0.15 * ToDouble(candidate.IdPattern)
            + 0.20 * ToDouble(candidate.TypeExact)
            + 0.10 * ToDouble(candidate.TypeSemanticStrong) * (1 - ToDouble(candidate.TypeExact))
            + 0.12 * ToDouble(candidate.TargetIsPk)
            + 0.08 * ToDouble(candidate.TargetIsUq) * (1 - ToDouble(candidate.TargetIsPk))
            + 0.05 * ToDouble(indexedSource)
            - 0.20 * ToDouble(ambiguousTargets)
            - 0.15 * ToDouble(genericName);

        return Math.Clamp(score, 0d, 1d);
    }

    private static double RoundScore(double score)
    {
        return Math.Round(score, 4, MidpointRounding.ToEven);
    }

    private static double ToDouble(bool value) => value ? 1d : 0d;

    private static bool IsAmbiguous(IReadOnlyList<CandidateTarget> orderedCandidates)
    {
        if (orderedCandidates.Count < 2)
        {
            return false;
        }

        CandidateTarget first = orderedCandidates[0];
        CandidateTarget second = orderedCandidates[1];

        return Math.Abs(first.Score - second.Score) < 0.0500
            && first.ExactEntityMatch == second.ExactEntityMatch
            && first.TargetIsPk == second.TargetIsPk;
    }

    private static SchemaIssue CreateIssue(
        TableMetadata sourceTable,
        ColumnMetadata sourceColumn,
        CandidateTarget best,
        double confidence,
        bool isAmbiguous,
        IReadOnlyList<CandidateTarget> topCandidates
    )
    {
        string title = "Missing FK by naming";
        string message = isAmbiguous
            ? $"A coluna '{sourceTable.FullName}.{sourceColumn.Name}' possui múltiplos destinos compatíveis para FK inferida."
            : $"A coluna '{sourceTable.FullName}.{sourceColumn.Name}' sugere FK ausente para '{best.TargetTable.FullName}.{best.TargetColumn.Name}'.";

        List<SchemaEvidence> evidence =
        [
            SchemaEvidenceFactory.NamingMatch("entityMatch", best.ExactEntityMatch ? "Exact" : "Synonym", 1.0),
            SchemaEvidenceFactory.TypeCompatibility("targetType", best.TypeExact ? "Exact" : "SemanticStrong", 0.95),
            SchemaEvidenceFactory.ConstraintTopology("targetKind", best.TargetIsPk ? "PrimaryKey" : "UniqueConstraint", 0.90),
            SchemaEvidenceFactory.MetadataFact("targetSchema", best.TargetTable.Schema, 0.90),
            SchemaEvidenceFactory.MetadataFact("targetTable", best.TargetTable.Name, 0.90),
            SchemaEvidenceFactory.MetadataFact("targetColumn", best.TargetColumn.Name, 0.90),
        ];

        if (isAmbiguous)
        {
            evidence.Add(
                SchemaEvidenceFactory.Ambiguity(
                    "candidateTargets",
                    string.Join(", ", topCandidates.Select(candidate => candidate.TargetTableFullName)),
                    0.85
                )
            );
        }

        return new SchemaIssue(
            IssueId: ComputeIssueId(
                SchemaRuleCode.MISSING_FK,
                SchemaTargetType.Column,
                sourceTable.Schema,
                sourceTable.Name,
                sourceColumn.Name,
                null,
                title,
                message,
                confidence,
                isAmbiguous
            ),
            RuleCode: SchemaRuleCode.MISSING_FK,
            Severity: DetermineSeverity(confidence, isAmbiguous),
            Confidence: confidence,
            TargetType: SchemaTargetType.Column,
            SchemaName: sourceTable.Schema,
            TableName: sourceTable.Name,
            ColumnName: sourceColumn.Name,
            ConstraintName: null,
            Title: title,
            Message: message,
            Evidence: evidence,
            Suggestions: [],
            IsAmbiguous: isAmbiguous
        );
    }

    private static SchemaIssueSeverity DetermineSeverity(double confidence, bool isAmbiguous)
    {
        if (isAmbiguous)
        {
            return SchemaIssueSeverity.Warning;
        }

        return confidence >= 0.8500 ? SchemaIssueSeverity.Critical : SchemaIssueSeverity.Warning;
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

    [GeneratedRegex("^(col|campo|field|tmp|test|data|valor|misc|x|y|z)[0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex GenericNamePattern();

    private sealed record CandidateTarget(
        string TargetTableFullName,
        TableMetadata TargetTable,
        ColumnMetadata TargetColumn,
        bool ExactEntityMatch,
        bool SynonymEntityMatch,
        bool IdPattern,
        bool TypeExact,
        bool TypeSemanticStrong,
        bool TargetIsPk,
        bool TargetIsUq,
        bool IndexedSource,
        bool GenericName,
        double Score
    );
}
