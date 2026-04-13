using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaAnalysisEnumContractsTests
{
    [Fact]
    public void SchemaRuleCode_MatchesNormativeClosedList()
    {
        string[] expected =
        [
            nameof(SchemaRuleCode.FK_CATALOG_INCONSISTENT),
            nameof(SchemaRuleCode.MISSING_FK),
            nameof(SchemaRuleCode.NAMING_CONVENTION_VIOLATION),
            nameof(SchemaRuleCode.LOW_SEMANTIC_NAME),
            nameof(SchemaRuleCode.MISSING_REQUIRED_COMMENT),
            nameof(SchemaRuleCode.NF1_HINT_MULTI_VALUED),
            nameof(SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY),
            nameof(SchemaRuleCode.NF3_HINT_TRANSITIVE_DEPENDENCY),
        ];

        Assert.Equal(expected, Enum.GetNames<SchemaRuleCode>());
    }

    [Fact]
    public void Enums_ExposeExpectedCanonicalNames()
    {
        Assert.Equal(
            [
                nameof(SchemaAnalysisStatus.Completed),
                nameof(SchemaAnalysisStatus.CompletedWithWarnings),
                nameof(SchemaAnalysisStatus.Partial),
                nameof(SchemaAnalysisStatus.Cancelled),
                nameof(SchemaAnalysisStatus.Failed),
            ],
            Enum.GetNames<SchemaAnalysisStatus>()
        );

        Assert.Equal(
            [nameof(SchemaIssueSeverity.Info), nameof(SchemaIssueSeverity.Warning), nameof(SchemaIssueSeverity.Critical)],
            Enum.GetNames<SchemaIssueSeverity>()
        );

        Assert.Equal(
            [
                nameof(SchemaTargetType.Schema),
                nameof(SchemaTargetType.Table),
                nameof(SchemaTargetType.Column),
                nameof(SchemaTargetType.Constraint),
            ],
            Enum.GetNames<SchemaTargetType>()
        );

        Assert.Equal(
            [
                nameof(EvidenceKind.MetadataFact),
                nameof(EvidenceKind.NamingMatch),
                nameof(EvidenceKind.TypeCompatibility),
                nameof(EvidenceKind.ConstraintTopology),
                nameof(EvidenceKind.PolicyRequirement),
                nameof(EvidenceKind.Ambiguity),
                nameof(EvidenceKind.ProviderLimitation),
                nameof(EvidenceKind.ThresholdDecision),
                nameof(EvidenceKind.ExecutionBoundary),
            ],
            Enum.GetNames<EvidenceKind>()
        );

        Assert.Equal(
            [
                nameof(SqlCandidateSafety.NonDestructive),
                nameof(SqlCandidateSafety.PotentiallyDestructive),
                nameof(SqlCandidateSafety.Destructive),
            ],
            Enum.GetNames<SqlCandidateSafety>()
        );

        Assert.Equal(
            [
                nameof(NamingConvention.SnakeCase),
                nameof(NamingConvention.CamelCase),
                nameof(NamingConvention.PascalCase),
                nameof(NamingConvention.KebabCase),
                nameof(NamingConvention.MixedAllowed),
            ],
            Enum.GetNames<NamingConvention>()
        );

        Assert.Equal(
            [
                nameof(NormalizationStrictness.Conservative),
                nameof(NormalizationStrictness.Balanced),
                nameof(NormalizationStrictness.Aggressive),
            ],
            Enum.GetNames<NormalizationStrictness>()
        );

        Assert.Equal(
            [
                nameof(CandidateVisibility.Hidden),
                nameof(CandidateVisibility.VisibleReadOnly),
                nameof(CandidateVisibility.VisibleActionable),
            ],
            Enum.GetNames<CandidateVisibility>()
        );

        Assert.Equal(
            [
                nameof(RuleExecutionState.NotStarted),
                nameof(RuleExecutionState.Running),
                nameof(RuleExecutionState.Completed),
                nameof(RuleExecutionState.Skipped),
                nameof(RuleExecutionState.Failed),
                nameof(RuleExecutionState.TimedOut),
                nameof(RuleExecutionState.Cancelled),
            ],
            Enum.GetNames<RuleExecutionState>()
        );
    }
}
