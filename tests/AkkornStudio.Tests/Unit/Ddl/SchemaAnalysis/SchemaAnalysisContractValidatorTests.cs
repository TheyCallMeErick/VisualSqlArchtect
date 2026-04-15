using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Validation;

namespace AkkornStudio.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaAnalysisContractValidatorTests
{
    [Fact]
    public void Validate_Fails_WhenEvidenceIsEmpty()
    {
        SchemaAnalysisContractValidator validator = new();
        var result = SchemaAnalysisContractValidatorTestData.CreateValidResult(
            SchemaAnalysisContractValidatorTestData.CreateIssue(evidence: [])
        );

        var errors = validator.Validate(result, SchemaAnalysisContractValidatorTestData.CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-EVIDENCE-EMPTY");
    }

    [Fact]
    public void Validate_Fails_WhenSuggestionsExceedProfileLimit()
    {
        SchemaAnalysisContractValidator validator = new();
        var issue = SchemaAnalysisContractValidatorTestData.CreateIssue(
            suggestions:
            [
                SchemaAnalysisContractValidatorTestData.CreateSuggestion("s1"),
                SchemaAnalysisContractValidatorTestData.CreateSuggestion("s2"),
                SchemaAnalysisContractValidatorTestData.CreateSuggestion("s3"),
            ]
        );
        var result = SchemaAnalysisContractValidatorTestData.CreateValidResult(issue);

        var errors = validator.Validate(result, SchemaAnalysisContractValidatorTestData.CreateProfile(maxSuggestionsPerIssue: 2));

        Assert.Contains(errors, e => e.Code == "ANL-VAL-MAX-SUGGESTIONS");
    }

    [Fact]
    public void Validate_Fails_WhenIssuesExceedProfileLimit()
    {
        SchemaAnalysisContractValidator validator = new();
        var result = SchemaAnalysisContractValidatorTestData.CreateValidResult([
            SchemaAnalysisContractValidatorTestData.CreateIssue(),
            SchemaAnalysisContractValidatorTestData.CreateIssue(issueId: "i-2")
        ]);

        var errors = validator.Validate(result, SchemaAnalysisContractValidatorTestData.CreateProfile(maxIssues: 1));

        Assert.Contains(errors, e => e.Code == "ANL-VAL-MAX-ISSUES");
    }

    [Fact]
    public void Validate_Fails_WhenColumnTargetMissesTableOrColumnName()
    {
        SchemaAnalysisContractValidator validator = new();
        var issue = SchemaAnalysisContractValidatorTestData.CreateIssue(targetType: SchemaTargetType.Column, tableName: null, columnName: "");
        var result = SchemaAnalysisContractValidatorTestData.CreateValidResult(issue);

        var errors = validator.Validate(result, SchemaAnalysisContractValidatorTestData.CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-TARGET-COLUMN");
    }

    [Fact]
    public void Validate_Fails_WhenConstraintTargetMissesConstraintName()
    {
        SchemaAnalysisContractValidator validator = new();
        var issue = SchemaAnalysisContractValidatorTestData.CreateIssue(targetType: SchemaTargetType.Constraint, constraintName: " ");
        var result = SchemaAnalysisContractValidatorTestData.CreateValidResult(issue);

        var errors = validator.Validate(result, SchemaAnalysisContractValidatorTestData.CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-TARGET-CONSTRAINT");
    }

    [Fact]
    public void Validate_Fails_WhenIssueConfidenceIsNotRoundedToEven4()
    {
        SchemaAnalysisContractValidator validator = new();
        var issue = SchemaAnalysisContractValidatorTestData.CreateIssue(confidence: 0.12345d);
        var result = SchemaAnalysisContractValidatorTestData.CreateValidResult(issue);

        var errors = validator.Validate(result, SchemaAnalysisContractValidatorTestData.CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-CONFIDENCE-ROUNDING");
    }

    [Fact]
    public void Validate_Fails_WhenIsAutoApplicableWithUnsafeCandidate()
    {
        SchemaAnalysisContractValidator validator = new();
        var candidate = SchemaAnalysisContractValidatorTestData.CreateCandidate(
            safety: SqlCandidateSafety.PotentiallyDestructive,
            isAutoApplicable: true
        );
        var issue = SchemaAnalysisContractValidatorTestData.CreateIssue(
            suggestions: [SchemaAnalysisContractValidatorTestData.CreateSuggestion("s-1", [candidate])]
        );
        var result = SchemaAnalysisContractValidatorTestData.CreateValidResult(issue);

        var errors = validator.Validate(result, SchemaAnalysisContractValidatorTestData.CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-AUTO-APPLICABLE-SAFETY");
    }

    [Fact]
    public void Validate_Fails_WhenVisibleActionableWithUnsafeCandidate()
    {
        SchemaAnalysisContractValidator validator = new();
        var candidate = SchemaAnalysisContractValidatorTestData.CreateCandidate(
            safety: SqlCandidateSafety.PotentiallyDestructive,
            visibility: CandidateVisibility.VisibleActionable
        );
        var issue = SchemaAnalysisContractValidatorTestData.CreateIssue(
            suggestions: [SchemaAnalysisContractValidatorTestData.CreateSuggestion("s-1", [candidate])]
        );
        var result = SchemaAnalysisContractValidatorTestData.CreateValidResult(issue);

        var errors = validator.Validate(result, SchemaAnalysisContractValidatorTestData.CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-VISIBILITY-SAFETY");
    }

    [Fact]
    public void Validate_Fails_WhenCandidatePreconditionsAreEmpty()
    {
        SchemaAnalysisContractValidator validator = new();
        var candidate = SchemaAnalysisContractValidatorTestData.CreateCandidate(preconditionsSql: []);
        var issue = SchemaAnalysisContractValidatorTestData.CreateIssue(
            suggestions: [SchemaAnalysisContractValidatorTestData.CreateSuggestion("s-1", [candidate])]
        );
        var result = SchemaAnalysisContractValidatorTestData.CreateValidResult(issue);

        var errors = validator.Validate(result, SchemaAnalysisContractValidatorTestData.CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-PRECONDITIONS-REQUIRED");
    }

    [Fact]
    public void EnsureValid_DoesNotThrow_ForValidPayload()
    {
        SchemaAnalysisContractValidator validator = new();
        var result = SchemaAnalysisContractValidatorTestData.CreateValidResult(
            SchemaAnalysisContractValidatorTestData.CreateIssue()
        );

        Exception? exception = Record.Exception(() =>
            validator.EnsureValid(result, SchemaAnalysisContractValidatorTestData.CreateProfile())
        );

        Assert.Null(exception);
    }
}
