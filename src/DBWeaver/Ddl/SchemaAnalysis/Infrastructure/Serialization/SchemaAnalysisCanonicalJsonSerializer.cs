using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Infrastructure.Serialization;

public sealed class SchemaAnalysisCanonicalJsonSerializer
{
    private static readonly JsonSerializerOptions SerializationOptions = CreateOptions();

    public string SerializeProfileCanonical(SchemaAnalysisProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        SchemaAnalysisProfile normalized = NormalizeProfile(profile);
        JsonNode? node = JsonSerializer.SerializeToNode(normalized, SerializationOptions);
        return SerializeCanonical(node);
    }

    public string SerializeResultCanonical(SchemaAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        SchemaAnalysisResult normalized = NormalizeResult(result);
        JsonNode? node = JsonSerializer.SerializeToNode(normalized, SerializationOptions);
        return SerializeCanonical(node);
    }

    private static SchemaAnalysisProfile NormalizeProfile(SchemaAnalysisProfile profile)
    {
        IReadOnlyDictionary<SchemaRuleCode, SchemaRuleSetting> sortedRuleSettings = profile
            .RuleSettings.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        IReadOnlyList<IReadOnlyList<string>> sortedSynonymGroups = profile
            .SynonymGroups.Select(group => (IReadOnlyList<string>)NormalizeSet(group).ToList())
            .OrderBy(group => string.Join("\u001f", group), StringComparer.Ordinal)
            .ToList();

        return profile with
        {
            RequiredCommentTargets = NormalizeSet(profile.RequiredCommentTargets),
            LowQualityNameDenylist = NormalizeSet(profile.LowQualityNameDenylist),
            NameAllowlist = NormalizeSet(profile.NameAllowlist),
            SemiStructuredPayloadAllowlist = NormalizeSet(profile.SemiStructuredPayloadAllowlist),
            SynonymGroups = sortedSynonymGroups,
            RuleSettings = sortedRuleSettings,
        };
    }

    private static SchemaAnalysisResult NormalizeResult(SchemaAnalysisResult result)
    {
        IReadOnlyList<SchemaIssue> normalizedIssues = result
            .Issues.Select(NormalizeIssue)
            .ToList();

        IReadOnlyDictionary<SchemaRuleCode, int> sortedPerRule = result
            .Summary.PerRuleCount.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        IReadOnlyDictionary<string, int> sortedPerTable = result
            .Summary.PerTableCount.OrderBy(pair => ToNfc(pair.Key), StringComparer.Ordinal)
            .ToDictionary(pair => ToNfc(pair.Key), pair => pair.Value);

        SchemaAnalysisSummary normalizedSummary = result.Summary with
        {
            PerRuleCount = sortedPerRule,
            PerTableCount = sortedPerTable,
        };

        return result with
        {
            AnalysisId = ToNfc(result.AnalysisId),
            DatabaseName = ToNfc(result.DatabaseName),
            MetadataFingerprint = ToNfc(result.MetadataFingerprint),
            ProfileContentHash = ToNfc(result.ProfileContentHash),
            Issues = normalizedIssues,
            Diagnostics = result.Diagnostics.Select(NormalizeDiagnostic).ToList(),
            Summary = normalizedSummary,
        };
    }

    private static SchemaIssue NormalizeIssue(SchemaIssue issue)
    {
        return issue with
        {
            IssueId = ToNfc(issue.IssueId),
            SchemaName = ToNullableName(issue.SchemaName),
            TableName = ToNullableName(issue.TableName),
            ColumnName = ToNullableName(issue.ColumnName),
            ConstraintName = ToNullableName(issue.ConstraintName),
            Title = ToNfc(issue.Title),
            Message = ToNfc(issue.Message),
            Evidence = issue.Evidence.Select(NormalizeEvidence).ToList(),
            Suggestions = issue.Suggestions.Select(NormalizeSuggestion).ToList(),
        };
    }

    private static SchemaSuggestion NormalizeSuggestion(SchemaSuggestion suggestion)
    {
        return suggestion with
        {
            SuggestionId = ToNfc(suggestion.SuggestionId),
            Title = ToNfc(suggestion.Title),
            Description = ToNfc(suggestion.Description),
            SqlCandidates = suggestion.SqlCandidates.Select(NormalizeCandidate).ToList(),
        };
    }

    private static SqlFixCandidate NormalizeCandidate(SqlFixCandidate candidate)
    {
        return candidate with
        {
            CandidateId = ToNfc(candidate.CandidateId),
            Title = ToNfc(candidate.Title),
            Sql = ToNfc(candidate.Sql),
            PreconditionsSql = candidate.PreconditionsSql.Select(ToNfc).ToList(),
            Notes = candidate.Notes.Select(ToNfc).ToList(),
        };
    }

    private static SchemaEvidence NormalizeEvidence(SchemaEvidence evidence)
    {
        return evidence with
        {
            Key = ToNfc(evidence.Key),
            Value = ToNfc(evidence.Value),
            SourcePath = ToNullableText(evidence.SourcePath),
        };
    }

    private static SchemaRuleExecutionDiagnostic NormalizeDiagnostic(
        SchemaRuleExecutionDiagnostic diagnostic
    )
    {
        return diagnostic with
        {
            Code = ToNfc(diagnostic.Code),
            Message = ToNfc(diagnostic.Message),
        };
    }

    private static IReadOnlyList<string> NormalizeSet(IEnumerable<string> values)
    {
        return values
            .Select(ToNfc)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
    }

    private static string SerializeCanonical(JsonNode? node)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalNode(writer, node);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonicalNode(Utf8JsonWriter writer, JsonNode? node)
    {
        if (node is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (node is JsonObject jsonObject)
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, JsonNode?> pair in jsonObject.OrderBy(
                pair => pair.Key,
                StringComparer.Ordinal
            ))
            {
                writer.WritePropertyName(ToNfc(pair.Key));
                WriteCanonicalNode(writer, pair.Value);
            }
            writer.WriteEndObject();
            return;
        }

        if (node is JsonArray jsonArray)
        {
            writer.WriteStartArray();
            foreach (JsonNode? item in jsonArray)
            {
                WriteCanonicalNode(writer, item);
            }
            writer.WriteEndArray();
            return;
        }

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out string? stringValue))
            {
                writer.WriteStringValue(ToNfc(stringValue));
                return;
            }

            if (jsonValue.TryGetValue<double>(out double doubleValue))
            {
                writer.WriteRawValue(
                    doubleValue.ToString("G17", CultureInfo.InvariantCulture),
                    skipInputValidation: true
                );
                return;
            }

            jsonValue.WriteTo(writer);
            return;
        }

        node.WriteTo(writer);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string ToNfc(string value) => value.Normalize(NormalizationForm.FormC);

    private static string? ToNullableName(string? value)
    {
        if (value is null)
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed.Normalize(NormalizationForm.FormC);
    }

    private static string? ToNullableText(string? value)
    {
        if (value is null)
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed.Normalize(NormalizationForm.FormC);
    }
}
