using System.Globalization;
using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Processing;

public sealed class SchemaSuggestionFactory
{
    public IReadOnlyList<SchemaSuggestion> CreateSuggestions(
        SchemaIssue issue,
        DatabaseProvider provider,
        SchemaAnalysisProfile profile
    )
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(profile);

        List<SchemaSuggestion> suggestions = issue.RuleCode switch
        {
            SchemaRuleCode.MISSING_FK => BuildMissingFkSuggestions(issue, provider),
            SchemaRuleCode.MISSING_REQUIRED_COMMENT => BuildMissingRequiredCommentSuggestions(issue, provider),
            SchemaRuleCode.NAMING_CONVENTION_VIOLATION => BuildNamingSuggestions(issue),
            SchemaRuleCode.LOW_SEMANTIC_NAME => BuildLowSemanticNameSuggestions(issue),
            SchemaRuleCode.NF1_HINT_MULTI_VALUED => BuildNormalizationSuggestions(issue, "Avaliar decomposição do atributo em estrutura atômica."),
            SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY => BuildNormalizationSuggestions(issue, "Avaliar mover o atributo para entidade dependente do componente parcial."),
            SchemaRuleCode.NF3_HINT_TRANSITIVE_DEPENDENCY => BuildNormalizationSuggestions(issue, "Avaliar mover o atributo descritivo para a tabela de referência correspondente."),
            _ => [],
        };

        return suggestions
            .OrderByDescending(static suggestion => suggestion.Confidence)
            .ThenBy(static suggestion => suggestion.Title, StringComparer.Ordinal)
            .ThenBy(static suggestion => suggestion.SuggestionId, StringComparer.Ordinal)
            .Take(profile.MaxSuggestionsPerIssue)
            .ToList();
    }

    private static List<SchemaSuggestion> BuildMissingFkSuggestions(SchemaIssue issue, DatabaseProvider provider)
    {
        string qualifiedColumn = BuildQualifiedColumn(issue);
        return
        [
            CreateSuggestion(
                issue,
                "Review inferred foreign key",
                $"Revisar a coluna '{qualifiedColumn}' e confirmar se ela deve ser promovida a FK explícita.",
                issue.Confidence,
                []
            ),
            CreateSuggestion(
                issue,
                "Validate relationship cardinality",
                $"Confirmar cardinalidade, nulabilidade e comportamento de delete/update antes de materializar a relação em {provider}.",
                Math.Max(0d, issue.Confidence - 0.1000),
                []
            ),
        ];
    }

    private static List<SchemaSuggestion> BuildMissingRequiredCommentSuggestions(SchemaIssue issue, DatabaseProvider provider)
    {
        string target = BuildIssueTarget(issue);
        return
        [
            CreateSuggestion(
                issue,
                "Add technical comment",
                $"Adicionar comentário técnico objetivo para '{target}' compatível com o provider {provider}.",
                issue.Confidence,
                []
            ),
        ];
    }

    private static List<SchemaSuggestion> BuildNamingSuggestions(SchemaIssue issue)
    {
        string target = BuildIssueTarget(issue);
        return
        [
            CreateSuggestion(
                issue,
                "Rename to configured convention",
                $"Renomear '{target}' para aderir à convenção configurada sem alterar o significado técnico.",
                issue.Confidence,
                []
            ),
        ];
    }

    private static List<SchemaSuggestion> BuildLowSemanticNameSuggestions(SchemaIssue issue)
    {
        string target = BuildIssueTarget(issue);
        return
        [
            CreateSuggestion(
                issue,
                "Use a more specific identifier",
                $"Substituir '{target}' por nome técnico mais específico e observável no schema.",
                issue.Confidence,
                []
            ),
        ];
    }

    private static List<SchemaSuggestion> BuildNormalizationSuggestions(SchemaIssue issue, string description)
    {
        return
        [
            CreateSuggestion(
                issue,
                "Review normalization shape",
                description,
                issue.Confidence,
                []
            ),
        ];
    }

    private static SchemaSuggestion CreateSuggestion(
        SchemaIssue issue,
        string title,
        string description,
        double confidence,
        IReadOnlyList<SqlFixCandidate> candidates
    )
    {
        double roundedConfidence = Math.Round(Math.Clamp(confidence, 0d, 1d), 4, MidpointRounding.ToEven);
        string suggestionId = ComputeSuggestionId(issue.IssueId, title, description, roundedConfidence);

        return new SchemaSuggestion(
            SuggestionId: suggestionId,
            Title: title,
            Description: description,
            Confidence: roundedConfidence,
            SqlCandidates: candidates
        );
    }

    private static string ComputeSuggestionId(
        string issueId,
        string title,
        string description,
        double confidence
    )
    {
        string payload = string.Join(
            "|",
            SchemaIssueTextNormalizer.NormalizeForHash(issueId),
            SchemaIssueTextNormalizer.NormalizeForHash(title),
            SchemaIssueTextNormalizer.NormalizeForHash(description),
            confidence.ToString("0.0000", CultureInfo.InvariantCulture)
        );

        return SchemaIssueTextNormalizer.ComputeSha256Hex(payload);
    }

    private static string BuildQualifiedColumn(SchemaIssue issue)
    {
        if (!string.IsNullOrWhiteSpace(issue.SchemaName)
            && !string.IsNullOrWhiteSpace(issue.TableName)
            && !string.IsNullOrWhiteSpace(issue.ColumnName))
        {
            return $"{issue.SchemaName}.{issue.TableName}.{issue.ColumnName}";
        }

        return BuildIssueTarget(issue);
    }

    private static string BuildIssueTarget(SchemaIssue issue)
    {
        return issue.TargetType switch
        {
            SchemaTargetType.Table => $"{issue.SchemaName}.{issue.TableName}",
            SchemaTargetType.Column => $"{issue.SchemaName}.{issue.TableName}.{issue.ColumnName}",
            SchemaTargetType.Constraint => issue.ConstraintName ?? issue.Title,
            _ => issue.Title,
        };
    }
}
