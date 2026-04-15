using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Validation;

public sealed class SchemaAnalysisProfileNormalizer
{
    public const int SupportedVersion = 1;

    public SchemaAnalysisProfileNormalizationResult Normalize(
        SchemaAnalysisProfile? rawProfile,
        int supportedVersion = SupportedVersion
    )
    {
        List<SchemaRuleExecutionDiagnostic> diagnostics = [];
        bool clamped = false;

        SchemaAnalysisProfile profile = rawProfile ?? CreateDefaultProfile();

        int version = profile.Version;
        if (version > supportedVersion)
        {
            version = supportedVersion;
            diagnostics.Add(
                new SchemaRuleExecutionDiagnostic(
                    Code: "ANL-SETTINGS-VERSION-FALLBACK",
                    Message:
                        "A versão de configuração excede a versão suportada; foi usada a última versão suportada.",
                    RuleCode: null,
                    State: RuleExecutionState.Completed,
                    IsFatal: false
                )
            );
        }

        NamingConvention namingConvention = NormalizeEnum(
            profile.NamingConvention,
            NamingConvention.SnakeCase,
            ref clamped
        );
        NormalizationStrictness normalizationStrictness = NormalizeEnum(
            profile.NormalizationStrictness,
            NormalizationStrictness.Balanced,
            ref clamped
        );

        Dictionary<SchemaRuleCode, SchemaRuleSetting> ruleSettings = CreateDefaultRuleSettings();
        foreach (KeyValuePair<SchemaRuleCode, SchemaRuleSetting> defaultEntry in ruleSettings.ToList())
        {
            if (!profile.RuleSettings.TryGetValue(defaultEntry.Key, out SchemaRuleSetting? configured))
            {
                clamped = true;
                continue;
            }

            if (configured is null)
            {
                clamped = true;
                continue;
            }

            ruleSettings[defaultEntry.Key] = new SchemaRuleSetting(
                Enabled: configured.Enabled,
                MinConfidence: ClampDouble(configured.MinConfidence, 0d, 1d, defaultEntry.Value.MinConfidence, ref clamped),
                MaxIssues: ClampInt(configured.MaxIssues, 1, int.MaxValue, defaultEntry.Value.MaxIssues, ref clamped)
            );
        }

        SchemaAnalysisProfile normalized = new(
            Version: version,
            Enabled: profile.Enabled,
            MinConfidenceGlobal: ClampDouble(profile.MinConfidenceGlobal, 0d, 1d, 0.55d, ref clamped),
            TimeoutMs: ClampInt(profile.TimeoutMs, 1, int.MaxValue, 15000, ref clamped),
            AllowPartialOnTimeout: profile.AllowPartialOnTimeout,
            AllowPartialOnRuleFailure: profile.AllowPartialOnRuleFailure,
            EnableParallelRules: profile.EnableParallelRules,
            MaxDegreeOfParallelism: ClampInt(profile.MaxDegreeOfParallelism, 1, int.MaxValue, 4, ref clamped),
            MaxIssues: ClampInt(profile.MaxIssues, 1, int.MaxValue, 5000, ref clamped),
            MaxSuggestionsPerIssue: ClampInt(profile.MaxSuggestionsPerIssue, 1, int.MaxValue, 3, ref clamped),
            NamingConvention: namingConvention,
            NormalizationStrictness: normalizationStrictness,
            RequiredCommentTargets: profile.RequiredCommentTargets ?? [],
            LowQualityNameDenylist: profile.LowQualityNameDenylist ?? [],
            NameAllowlist: profile.NameAllowlist ?? [],
            SynonymGroups: profile.SynonymGroups ?? [],
            SemiStructuredPayloadAllowlist: profile.SemiStructuredPayloadAllowlist ?? [],
            DebugDiagnostics: profile.DebugDiagnostics,
            RuleSettings: ruleSettings,
            CacheTtlSeconds: ClampInt(profile.CacheTtlSeconds, 0, int.MaxValue, 300, ref clamped)
        );

        if (clamped)
        {
            diagnostics.Add(
                new SchemaRuleExecutionDiagnostic(
                    Code: "ANL-SETTINGS-CLAMPED",
                    Message: "Configuração fora da faixa normativa foi ajustada para valor válido.",
                    RuleCode: null,
                    State: RuleExecutionState.Completed,
                    IsFatal: false
                )
            );
        }

        return new SchemaAnalysisProfileNormalizationResult(
            Profile: normalized,
            Diagnostics: diagnostics
                .OrderByDescending(static diagnostic => diagnostic.IsFatal)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToList()
        );
    }

    public static SchemaAnalysisProfile CreateDefaultProfile()
    {
        return new SchemaAnalysisProfile(
            Version: 1,
            Enabled: true,
            MinConfidenceGlobal: 0.55d,
            TimeoutMs: 15000,
            AllowPartialOnTimeout: true,
            AllowPartialOnRuleFailure: true,
            EnableParallelRules: true,
            MaxDegreeOfParallelism: 4,
            MaxIssues: 5000,
            MaxSuggestionsPerIssue: 3,
            NamingConvention: NamingConvention.SnakeCase,
            NormalizationStrictness: NormalizationStrictness.Balanced,
            RequiredCommentTargets: ["Table", "PrimaryKeyColumn", "ForeignKeyColumn"],
            LowQualityNameDenylist: ["tmp", "teste", "campo", "valor", "misc", "foo", "bar", "x", "y", "z"],
            NameAllowlist: [],
            SynonymGroups:
            [
                ["person", "pessoa"],
                ["customer", "cliente"],
                ["user", "usuario"],
            ],
            SemiStructuredPayloadAllowlist: [],
            DebugDiagnostics: false,
            RuleSettings: CreateDefaultRuleSettings(),
            CacheTtlSeconds: 300
        );
    }

    private static Dictionary<SchemaRuleCode, SchemaRuleSetting> CreateDefaultRuleSettings()
    {
        return new Dictionary<SchemaRuleCode, SchemaRuleSetting>
        {
            [SchemaRuleCode.FK_CATALOG_INCONSISTENT] = new(true, 0.75d, 1000),
            [SchemaRuleCode.MISSING_FK] = new(true, 0.65d, 1000),
            [SchemaRuleCode.NAMING_CONVENTION_VIOLATION] = new(true, 0.70d, 1000),
            [SchemaRuleCode.LOW_SEMANTIC_NAME] = new(true, 0.60d, 1000),
            [SchemaRuleCode.MISSING_REQUIRED_COMMENT] = new(true, 0.70d, 1000),
            [SchemaRuleCode.NF1_HINT_MULTI_VALUED] = new(true, 0.60d, 500),
            [SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY] = new(true, 0.65d, 500),
            [SchemaRuleCode.NF3_HINT_TRANSITIVE_DEPENDENCY] = new(true, 0.65d, 500),
        };
    }

    private static int ClampInt(int value, int min, int max, int fallback, ref bool clamped)
    {
        if (value < min || value > max)
        {
            clamped = true;
            return fallback;
        }

        return value;
    }

    private static double ClampDouble(double value, double min, double max, double fallback, ref bool clamped)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
        {
            clamped = true;
            return fallback;
        }

        return value;
    }

    private static TEnum NormalizeEnum<TEnum>(TEnum value, TEnum fallback, ref bool clamped)
        where TEnum : struct, Enum
    {
        if (Enum.IsDefined(value))
        {
            return value;
        }

        clamped = true;
        return fallback;
    }
}
