using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Normalization;

namespace AkkornStudio.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaTokenEquivalenceResolverTests
{
    [Fact]
    public void Resolve_UsesFirstSynonymGroup_WhenTokenAppearsInMultipleGroups()
    {
        SchemaTokenEquivalenceResolver resolver = new();

        SchemaTokenEquivalenceResolution resolution = resolver.Resolve(
            CreateProfile(
                synonymGroups:
                [
                    new List<string> { "person", "pessoa" },
                    new List<string> { "customer", "pessoa" },
                ]
            )
        );

        Assert.Equal("person", resolution.NormalizeToken("pessoa"));
        Assert.Contains(resolution.Diagnostics, diagnostic => diagnostic.Code == "ANL-SETTINGS-SYNONYM-CONFLICT");
    }

    [Fact]
    public void Resolve_AllowlistOverridesDenylist()
    {
        SchemaTokenEquivalenceResolver resolver = new();

        SchemaTokenEquivalenceResolution resolution = resolver.Resolve(
            CreateProfile(
                nameAllowlist: ["valor"],
                lowQualityNameDenylist: ["valor"]
            )
        );

        Assert.True(resolution.IsAllowlisted("valor"));
        Assert.False(resolution.IsDenylisted("valor"));
        Assert.Contains(
            resolution.Diagnostics,
            diagnostic => diagnostic.Code == "ANL-SETTINGS-ALLOWLIST-OVERRIDES-DENYLIST"
        );
    }

    [Fact]
    public void Resolve_ReturnsNoDiagnostics_WhenNoGroupsOrOverridesExist()
    {
        SchemaTokenEquivalenceResolver resolver = new();

        SchemaTokenEquivalenceResolution resolution = resolver.Resolve(
            CreateProfile(
                synonymGroups: [],
                nameAllowlist: [],
                lowQualityNameDenylist: ["tmp"]
            )
        );

        Assert.Empty(resolution.CanonicalSynonyms);
        Assert.Empty(resolution.Diagnostics);
        Assert.True(resolution.IsDenylisted("tmp"));
    }

    private static SchemaAnalysisProfile CreateProfile(
        IReadOnlyList<IReadOnlyList<string>>? synonymGroups = null,
        IReadOnlyList<string>? nameAllowlist = null,
        IReadOnlyList<string>? lowQualityNameDenylist = null
    ) =>
        new(
            Version: 1,
            Enabled: true,
            MinConfidenceGlobal: 0.55,
            TimeoutMs: 15000,
            AllowPartialOnTimeout: true,
            AllowPartialOnRuleFailure: true,
            EnableParallelRules: true,
            MaxDegreeOfParallelism: 4,
            MaxIssues: 5000,
            MaxSuggestionsPerIssue: 3,
            NamingConvention: NamingConvention.SnakeCase,
            NormalizationStrictness: NormalizationStrictness.Balanced,
            RequiredCommentTargets: ["Table"],
            LowQualityNameDenylist: lowQualityNameDenylist ?? ["tmp"],
            NameAllowlist: nameAllowlist ?? [],
            SynonymGroups: synonymGroups ?? [new List<string> { "person", "pessoa" }],
            SemiStructuredPayloadAllowlist: [],
            DebugDiagnostics: false,
            RuleSettings: new Dictionary<SchemaRuleCode, SchemaRuleSetting>
            {
                [SchemaRuleCode.MISSING_FK] = new SchemaRuleSetting(true, 0.65, 1000),
            },
            CacheTtlSeconds: 300
        );
}
