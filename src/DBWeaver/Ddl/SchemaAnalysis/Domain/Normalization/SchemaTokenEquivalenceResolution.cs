using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

public sealed record SchemaTokenEquivalenceResolution(
    IReadOnlyDictionary<string, string> CanonicalSynonyms,
    IReadOnlySet<string> Allowlist,
    IReadOnlySet<string> Denylist,
    IReadOnlyList<SchemaRuleExecutionDiagnostic> Diagnostics
)
{
    public string NormalizeToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return CanonicalSynonyms.TryGetValue(token, out string? canonical) ? canonical : token;
    }

    public bool IsAllowlisted(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Allowlist.Contains(NormalizeToken(token));
    }

    public bool IsDenylisted(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        string canonical = NormalizeToken(token);
        return !Allowlist.Contains(canonical) && Denylist.Contains(canonical);
    }
}
