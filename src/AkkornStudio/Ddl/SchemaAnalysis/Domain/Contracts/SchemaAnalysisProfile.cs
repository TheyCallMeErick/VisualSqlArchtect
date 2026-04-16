using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaAnalysisProfile(
    int Version,
    bool Enabled,
    double MinConfidenceGlobal,
    int TimeoutMs,
    bool AllowPartialOnTimeout,
    bool AllowPartialOnRuleFailure,
    bool EnableParallelRules,
    int MaxDegreeOfParallelism,
    int MaxIssues,
    int MaxSuggestionsPerIssue,
    NamingConvention NamingConvention,
    NormalizationStrictness NormalizationStrictness,
    IReadOnlyList<string> RequiredCommentTargets,
    IReadOnlyList<string> LowQualityNameDenylist,
    IReadOnlyList<string> NameAllowlist,
    IReadOnlyList<IReadOnlyList<string>> SynonymGroups,
    IReadOnlyList<string> SemiStructuredPayloadAllowlist,
    bool DebugDiagnostics,
    IReadOnlyDictionary<SchemaRuleCode, SchemaRuleSetting> RuleSettings,
    int CacheTtlSeconds
);
