using System.Globalization;
using System.Text;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Normalization;

public sealed class SchemaTokenEquivalenceResolver
{
    public SchemaTokenEquivalenceResolution Resolve(SchemaAnalysisProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Dictionary<string, string> canonicalSynonyms = new(StringComparer.Ordinal);
        List<SchemaRuleExecutionDiagnostic> diagnostics = [];
        bool synonymConflictRecorded = false;

        foreach (IReadOnlyList<string> group in profile.SynonymGroups)
        {
            if (group.Count == 0)
            {
                continue;
            }

            string? canonical = null;

            foreach (string rawToken in group)
            {
                string normalizedToken = NormalizeToken(rawToken);
                if (normalizedToken.Length == 0)
                {
                    continue;
                }

                canonical ??= normalizedToken;

                if (
                    canonicalSynonyms.TryGetValue(normalizedToken, out string? existingCanonical)
                    && !string.Equals(existingCanonical, canonical, StringComparison.Ordinal)
                )
                {
                    if (!synonymConflictRecorded)
                    {
                        diagnostics.Add(
                            new SchemaRuleExecutionDiagnostic(
                                Code: "ANL-SETTINGS-SYNONYM-CONFLICT",
                                Message:
                                    "Um token pertence a múltiplos grupos de sinônimos; prevaleceu o primeiro grupo.",
                                RuleCode: null,
                                State: RuleExecutionState.Completed,
                                IsFatal: false
                            )
                        );
                        synonymConflictRecorded = true;
                    }

                    continue;
                }

                canonicalSynonyms[normalizedToken] = canonical;
            }
        }

        HashSet<string> allowlist = NormalizeTokenSet(profile.NameAllowlist, canonicalSynonyms);
        HashSet<string> denylist = NormalizeTokenSet(profile.LowQualityNameDenylist, canonicalSynonyms);

        if (allowlist.Overlaps(denylist))
        {
            diagnostics.Add(
                new SchemaRuleExecutionDiagnostic(
                    Code: "ANL-SETTINGS-ALLOWLIST-OVERRIDES-DENYLIST",
                    Message: "A allowlist suprimiu um match da denylist.",
                    RuleCode: null,
                    State: RuleExecutionState.Completed,
                    IsFatal: false
                )
            );
        }

        return new SchemaTokenEquivalenceResolution(
            CanonicalSynonyms: canonicalSynonyms,
            Allowlist: allowlist,
            Denylist: denylist,
            Diagnostics: diagnostics
        );
    }

    private static HashSet<string> NormalizeTokenSet(
        IReadOnlyList<string> rawTokens,
        IReadOnlyDictionary<string, string> canonicalSynonyms
    )
    {
        HashSet<string> normalizedTokens = new(StringComparer.Ordinal);

        foreach (string rawToken in rawTokens)
        {
            string normalized = NormalizeToken(rawToken);
            if (normalized.Length == 0)
            {
                continue;
            }

            if (canonicalSynonyms.TryGetValue(normalized, out string? canonical))
            {
                normalizedTokens.Add(canonical);
                continue;
            }

            normalizedTokens.Add(normalized);
        }

        return normalizedTokens;
    }

    private static string NormalizeToken(string rawToken)
    {
        ArgumentNullException.ThrowIfNull(rawToken);

        string noDiacritics = RemoveDiacritics(rawToken).ToLowerInvariant();
        StringBuilder builder = new(noDiacritics.Length);

        foreach (char character in noDiacritics)
        {
            if (character is >= 'a' and <= 'z' || character is >= '0' and <= '9')
            {
                builder.Append(character);
            }
        }

        return SchemaTokenSingularizer.Singularize(builder.ToString());
    }

    private static string RemoveDiacritics(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(normalized.Length);

        foreach (char character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
