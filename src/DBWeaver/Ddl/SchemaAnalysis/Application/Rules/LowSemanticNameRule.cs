using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DBWeaver.Ddl.SchemaAnalysis.Application.Indexing;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Rules;

public sealed partial class LowSemanticNameRule : ISchemaAnalysisRule
{
    private readonly SchemaTokenEquivalenceResolver _equivalenceResolver = new();

    public SchemaRuleCode RuleCode => SchemaRuleCode.LOW_SEMANTIC_NAME;

    public Task<SchemaRuleExecutionResult> ExecuteAsync(
        SchemaAnalysisExecutionContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        SchemaTokenEquivalenceResolution equivalence = _equivalenceResolver.Resolve(context.Profile);
        List<SchemaIssue> issues = [];

        foreach (SchemaNormalizedNameIndexEntry entry in context.Indices.NormalizedNameIndex.Values.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.TargetType == SchemaTargetType.Constraint)
            {
                continue;
            }

            string? principalToken = entry.Tokens.PrincipalEntityToken;
            if (principalToken is null && !entry.Tokens.HasEntityTokens)
            {
                continue;
            }

            if (principalToken is null)
            {
                continue;
            }

            if (equivalence.IsAllowlisted(principalToken))
            {
                continue;
            }

            bool shouldEmit = equivalence.IsDenylisted(principalToken) || WeakNamePattern().IsMatch(principalToken);
            if (!shouldEmit)
            {
                continue;
            }

            string rawName = ResolveRawName(entry);
            string title = "Low semantic name";
            string message = $"O nome '{rawName}' possui baixa qualidade semântica técnica.";

            issues.Add(
                new SchemaIssue(
                    IssueId: ComputeIssueId(entry, title, message),
                    RuleCode: SchemaRuleCode.LOW_SEMANTIC_NAME,
                    Severity: SchemaIssueSeverity.Info,
                    Confidence: 0.7500,
                    TargetType: entry.TargetType,
                    SchemaName: entry.SchemaName,
                    TableName: entry.TableName,
                    ColumnName: entry.ColumnName,
                    ConstraintName: null,
                    Title: title,
                    Message: message,
                    Evidence:
                    [
                        new SchemaEvidence(
                            EvidenceKind.MetadataFact,
                            "principalToken",
                            principalToken,
                            1.0
                        ),
                    ],
                    Suggestions: [],
                    IsAmbiguous: false
                )
            );
        }

        return Task.FromResult(new SchemaRuleExecutionResult(issues, []));
    }

    private static string ResolveRawName(SchemaNormalizedNameIndexEntry entry)
    {
        return entry.TargetType switch
        {
            SchemaTargetType.Table => entry.TableName!,
            SchemaTargetType.Column => entry.ColumnName!,
            _ => entry.TableName ?? entry.ColumnName ?? string.Empty,
        };
    }

    private static string ComputeIssueId(
        SchemaNormalizedNameIndexEntry entry,
        string title,
        string message
    )
    {
        string payload = string.Join(
            "|",
            SchemaRuleCode.LOW_SEMANTIC_NAME,
            entry.TargetType,
            NormalizeText(entry.SchemaName),
            NormalizeText(entry.TableName),
            NormalizeText(entry.ColumnName),
            "∅",
            NormalizeText(title),
            NormalizeText(message),
            "0.7500",
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

    [GeneratedRegex("^(col|campo|field|tmp|test|data|valor|misc|x|y|z)[0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex WeakNamePattern();
}
