using AkkornStudio.Ddl.SchemaAnalysis.Application.Processing;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Validation;

namespace AkkornStudio.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaEvidenceFactoryTests
{
    [Theory]
    [InlineData(EvidenceKind.MetadataFact)]
    [InlineData(EvidenceKind.NamingMatch)]
    [InlineData(EvidenceKind.TypeCompatibility)]
    [InlineData(EvidenceKind.ConstraintTopology)]
    [InlineData(EvidenceKind.PolicyRequirement)]
    [InlineData(EvidenceKind.Ambiguity)]
    [InlineData(EvidenceKind.ProviderLimitation)]
    [InlineData(EvidenceKind.ThresholdDecision)]
    [InlineData(EvidenceKind.ExecutionBoundary)]
    public void Create_CreatesEvidence_ForEverySupportedKind(EvidenceKind kind)
    {
        SchemaEvidence evidence = kind switch
        {
            EvidenceKind.MetadataFact => SchemaEvidenceFactory.MetadataFact("key", "value", 1.0, "issue.evidence[0]"),
            EvidenceKind.NamingMatch => SchemaEvidenceFactory.NamingMatch("key", "value", 0.9),
            EvidenceKind.TypeCompatibility => SchemaEvidenceFactory.TypeCompatibility("key", "value", 0.8),
            EvidenceKind.ConstraintTopology => SchemaEvidenceFactory.ConstraintTopology("key", "value", 0.7),
            EvidenceKind.PolicyRequirement => SchemaEvidenceFactory.PolicyRequirement("key", "value", 0.6),
            EvidenceKind.Ambiguity => SchemaEvidenceFactory.Ambiguity("key", "value", 0.5),
            EvidenceKind.ProviderLimitation => SchemaEvidenceFactory.ProviderLimitation("key", "value", 0.4),
            EvidenceKind.ThresholdDecision => SchemaEvidenceFactory.ThresholdDecision("key", "value", 0.3),
            EvidenceKind.ExecutionBoundary => SchemaEvidenceFactory.ExecutionBoundary("key", "value", 0.2),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        Assert.Equal(kind, evidence.Kind);
        Assert.Equal("key", evidence.Key);
        Assert.Equal("value", evidence.Value);
        Assert.InRange(evidence.Weight, 0d, 1d);
    }

    [Fact]
    public void ContractValidator_AcceptsIssue_WhenFactoryBuildsMinimalEvidence()
    {
        SchemaAnalysisContractValidator validator = new();
        SchemaAnalysisResult result = SchemaAnalysisContractValidatorTestData.CreateValidResult(
            SchemaAnalysisContractValidatorTestData.CreateIssue(
                evidence: [SchemaEvidenceFactory.MetadataFact("score", "0.8500", 1.0)]
            )
        );

        IReadOnlyList<SchemaContractValidationError> errors = validator.Validate(
            result,
            SchemaAnalysisContractValidatorTestData.CreateProfile()
        );

        Assert.DoesNotContain(errors, error => error.Code == "ANL-VAL-EVIDENCE-EMPTY");
    }

    [Fact]
    public void ContractValidator_FailsIssue_WhenEvidenceIsMissing()
    {
        SchemaAnalysisContractValidator validator = new();
        SchemaAnalysisResult result = SchemaAnalysisContractValidatorTestData.CreateValidResult(
            SchemaAnalysisContractValidatorTestData.CreateIssue(evidence: [])
        );

        IReadOnlyList<SchemaContractValidationError> errors = validator.Validate(
            result,
            SchemaAnalysisContractValidatorTestData.CreateProfile()
        );

        Assert.Contains(errors, error => error.Code == "ANL-VAL-EVIDENCE-EMPTY");
    }
}
