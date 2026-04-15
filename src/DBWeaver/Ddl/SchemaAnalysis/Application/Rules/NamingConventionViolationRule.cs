using System.Security.Cryptography;
using System.Text;
using DBWeaver.Ddl.SchemaAnalysis.Application.Indexing;
using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Rules;

public sealed class NamingConventionViolationRule : ISchemaAnalysisRule
{
    private readonly SchemaNamingConventionValidator _validator = new();

    public SchemaRuleCode RuleCode => SchemaRuleCode.NAMING_CONVENTION_VIOLATION;

    public Task<SchemaRuleExecutionResult> ExecuteAsync(
        SchemaAnalysisExecutionContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Profile.NamingConvention == NamingConvention.MixedAllowed)
        {
            return Task.FromResult(new SchemaRuleExecutionResult([], []));
        }

        List<SchemaIssue> issues = [];

        foreach (SchemaNormalizedNameIndexEntry entry in context.Indices.NormalizedNameIndex.Values.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string rawName = ResolveRawName(entry);
            if (_validator.IsValid(rawName, context.Profile.NamingConvention))
            {
                continue;
            }

            string title = "Naming convention violation";
            string message = $"O nome '{rawName}' viola a convenção configurada '{context.Profile.NamingConvention}'.";

            issues.Add(
                new SchemaIssue(
                    IssueId: ComputeIssueId(entry, title, message),
                    RuleCode: SchemaRuleCode.NAMING_CONVENTION_VIOLATION,
                    Severity: SchemaIssueSeverity.Warning,
                    Confidence: 0.9000,
                    TargetType: entry.TargetType,
                    SchemaName: entry.SchemaName,
                    TableName: entry.TableName,
                    ColumnName: entry.ColumnName,
                    ConstraintName: entry.ConstraintName,
                    Title: title,
                    Message: message,
                    Evidence:
                    [
                        SchemaEvidenceFactory.PolicyRequirement(
                            "namingConvention",
                            context.Profile.NamingConvention.ToString(),
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
            SchemaTargetType.Constraint => entry.ConstraintName!,
            _ => entry.TableName ?? entry.ColumnName ?? entry.ConstraintName ?? string.Empty,
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
            SchemaRuleCode.NAMING_CONVENTION_VIOLATION,
            entry.TargetType,
            NormalizeText(entry.SchemaName),
            NormalizeText(entry.TableName),
            NormalizeText(entry.ColumnName),
            NormalizeText(entry.ConstraintName),
            NormalizeText(title),
            NormalizeText(message),
            "0.9000",
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
