using DBWeaver.Ddl.SchemaAnalysis.Application.Validation;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaAnalysisProfileNormalizerTests
{
    [Fact]
    public void Normalize_UsesDefaults_WhenProfileIsNull()
    {
        SchemaAnalysisProfileNormalizer normalizer = new();

        SchemaAnalysisProfileNormalizationResult result = normalizer.Normalize(null);

        Assert.Equal(1, result.Profile.Version);
        Assert.Equal(0.55d, result.Profile.MinConfidenceGlobal);
        Assert.Equal(8, result.Profile.RuleSettings.Count);
    }

    [Fact]
    public void Normalize_ClampsOutOfRangeValues_AndEmitsDiagnostic()
    {
        SchemaAnalysisProfileNormalizer normalizer = new();
        SchemaAnalysisProfile rawProfile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile() with
        {
            MinConfidenceGlobal = -1d,
            TimeoutMs = 0,
            MaxDegreeOfParallelism = 0,
            MaxIssues = 0,
            MaxSuggestionsPerIssue = 0,
            CacheTtlSeconds = -5,
        };

        SchemaAnalysisProfileNormalizationResult result = normalizer.Normalize(rawProfile);

        Assert.Equal(0.55d, result.Profile.MinConfidenceGlobal);
        Assert.Equal(15000, result.Profile.TimeoutMs);
        Assert.Equal(4, result.Profile.MaxDegreeOfParallelism);
        Assert.Equal(5000, result.Profile.MaxIssues);
        Assert.Equal(3, result.Profile.MaxSuggestionsPerIssue);
        Assert.Equal(300, result.Profile.CacheTtlSeconds);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ANL-SETTINGS-CLAMPED");
    }

    [Fact]
    public void Normalize_FallsBackInvalidEnums_AndEmitsDiagnostic()
    {
        SchemaAnalysisProfileNormalizer normalizer = new();
        SchemaAnalysisProfile rawProfile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile() with
        {
            NamingConvention = (NamingConvention)999,
            NormalizationStrictness = (NormalizationStrictness)999,
        };

        SchemaAnalysisProfileNormalizationResult result = normalizer.Normalize(rawProfile);

        Assert.Equal(NamingConvention.SnakeCase, result.Profile.NamingConvention);
        Assert.Equal(NormalizationStrictness.Balanced, result.Profile.NormalizationStrictness);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ANL-SETTINGS-CLAMPED");
    }

    [Fact]
    public void Normalize_FallsBackVersion_WhenAboveSupported()
    {
        SchemaAnalysisProfileNormalizer normalizer = new();
        SchemaAnalysisProfile rawProfile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile() with
        {
            Version = 99,
        };

        SchemaAnalysisProfileNormalizationResult result = normalizer.Normalize(rawProfile, supportedVersion: 1);

        Assert.Equal(1, result.Profile.Version);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ANL-SETTINGS-VERSION-FALLBACK");
    }

    [Fact]
    public void Normalize_CompletesMissingRuleSettings_UsingDefaults()
    {
        SchemaAnalysisProfileNormalizer normalizer = new();
        SchemaAnalysisProfile rawProfile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile() with
        {
            RuleSettings = new Dictionary<SchemaRuleCode, SchemaRuleSetting>
            {
                [SchemaRuleCode.MISSING_FK] = new(true, 0.40d, 12),
            },
        };

        SchemaAnalysisProfileNormalizationResult result = normalizer.Normalize(rawProfile);

        Assert.Equal(Enum.GetValues<SchemaRuleCode>().Length, result.Profile.RuleSettings.Count);
        Assert.Equal(12, result.Profile.RuleSettings[SchemaRuleCode.MISSING_FK].MaxIssues);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ANL-SETTINGS-CLAMPED");
    }
}
