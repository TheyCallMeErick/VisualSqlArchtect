using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Validation;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaAnalysisContractValidatorTests
{
    [Fact]
    public void Validate_Fails_WhenEvidenceIsEmpty()
    {
        SchemaAnalysisContractValidator validator = new();
        SchemaAnalysisResult result = CreateValidResult(
            CreateIssue(evidence: [])
        );

        IReadOnlyList<SchemaContractValidationError> errors = validator.Validate(result, CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-EVIDENCE-EMPTY");
    }

    [Fact]
    public void Validate_Fails_WhenSuggestionsExceedProfileLimit()
    {
        SchemaAnalysisContractValidator validator = new();
        SchemaIssue issue = CreateIssue(
            suggestions:
            [
                CreateSuggestion("s1"),
                CreateSuggestion("s2"),
                CreateSuggestion("s3"),
            ]
        );
        SchemaAnalysisResult result = CreateValidResult(issue);

        IReadOnlyList<SchemaContractValidationError> errors = validator.Validate(
            result,
            CreateProfile(maxSuggestionsPerIssue: 2)
        );

        Assert.Contains(errors, e => e.Code == "ANL-VAL-MAX-SUGGESTIONS");
    }

    [Fact]
    public void Validate_Fails_WhenIssuesExceedProfileLimit()
    {
        SchemaAnalysisContractValidator validator = new();
        SchemaAnalysisResult result = CreateValidResult([CreateIssue(), CreateIssue(issueId: "i-2")]);

        IReadOnlyList<SchemaContractValidationError> errors = validator.Validate(
            result,
            CreateProfile(maxIssues: 1)
        );

        Assert.Contains(errors, e => e.Code == "ANL-VAL-MAX-ISSUES");
    }

    [Fact]
    public void Validate_Fails_WhenColumnTargetMissesTableOrColumnName()
    {
        SchemaAnalysisContractValidator validator = new();
        SchemaIssue issue = CreateIssue(targetType: SchemaTargetType.Column, tableName: null, columnName: "");
        SchemaAnalysisResult result = CreateValidResult(issue);

        IReadOnlyList<SchemaContractValidationError> errors = validator.Validate(result, CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-TARGET-COLUMN");
    }

    [Fact]
    public void Validate_Fails_WhenConstraintTargetMissesConstraintName()
    {
        SchemaAnalysisContractValidator validator = new();
        SchemaIssue issue = CreateIssue(
            targetType: SchemaTargetType.Constraint,
            constraintName: " "
        );
        SchemaAnalysisResult result = CreateValidResult(issue);

        IReadOnlyList<SchemaContractValidationError> errors = validator.Validate(result, CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-TARGET-CONSTRAINT");
    }

    [Fact]
    public void Validate_Fails_WhenIssueConfidenceIsNotRoundedToEven4()
    {
        SchemaAnalysisContractValidator validator = new();
        SchemaIssue issue = CreateIssue(confidence: 0.12345d);
        SchemaAnalysisResult result = CreateValidResult(issue);

        IReadOnlyList<SchemaContractValidationError> errors = validator.Validate(result, CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-CONFIDENCE-ROUNDING");
    }

    [Fact]
    public void Validate_Fails_WhenIsAutoApplicableWithUnsafeCandidate()
    {
        SchemaAnalysisContractValidator validator = new();
        SqlFixCandidate candidate = CreateCandidate(
            safety: SqlCandidateSafety.PotentiallyDestructive,
            isAutoApplicable: true
        );
        SchemaIssue issue = CreateIssue(suggestions: [CreateSuggestion("s-1", [candidate])]);
        SchemaAnalysisResult result = CreateValidResult(issue);

        IReadOnlyList<SchemaContractValidationError> errors = validator.Validate(result, CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-AUTO-APPLICABLE-SAFETY");
    }

    [Fact]
    public void Validate_Fails_WhenVisibleActionableWithUnsafeCandidate()
    {
        SchemaAnalysisContractValidator validator = new();
        SqlFixCandidate candidate = CreateCandidate(
            safety: SqlCandidateSafety.PotentiallyDestructive,
            visibility: CandidateVisibility.VisibleActionable
        );
        SchemaIssue issue = CreateIssue(suggestions: [CreateSuggestion("s-1", [candidate])]);
        SchemaAnalysisResult result = CreateValidResult(issue);

        IReadOnlyList<SchemaContractValidationError> errors = validator.Validate(result, CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-VISIBILITY-SAFETY");
    }

    [Fact]
    public void Validate_Fails_WhenCandidatePreconditionsAreEmpty()
    {
        SchemaAnalysisContractValidator validator = new();
        SqlFixCandidate candidate = CreateCandidate(preconditionsSql: []);
        SchemaIssue issue = CreateIssue(suggestions: [CreateSuggestion("s-1", [candidate])]);
        SchemaAnalysisResult result = CreateValidResult(issue);

        IReadOnlyList<SchemaContractValidationError> errors = validator.Validate(result, CreateProfile());

        Assert.Contains(errors, e => e.Code == "ANL-VAL-PRECONDITIONS-REQUIRED");
    }

    [Fact]
    public void EnsureValid_DoesNotThrow_ForValidPayload()
    {
        SchemaAnalysisContractValidator validator = new();
        SchemaAnalysisResult result = CreateValidResult(CreateIssue());

        Exception? exception = Record.Exception(() => validator.EnsureValid(result, CreateProfile()));

        Assert.Null(exception);
    }

    private static SchemaAnalysisResult CreateValidResult(SchemaIssue issue) =>
        CreateValidResult([issue]);

    private static SchemaAnalysisResult CreateValidResult(IReadOnlyList<SchemaIssue> issues)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new SchemaAnalysisResult(
            AnalysisId: "analysis-1",
            Status: SchemaAnalysisStatus.Completed,
            Provider: DatabaseProvider.Postgres,
            DatabaseName: "db",
            StartedAtUtc: now,
            CompletedAtUtc: now,
            DurationMs: 1,
            MetadataFingerprint: "fingerprint",
            ProfileContentHash: "profile",
            ProfileVersion: 1,
            PartialState: new SchemaAnalysisPartialState(false, "NONE", 8, 8),
            Issues: issues,
            Diagnostics: [],
            Summary: new SchemaAnalysisSummary(
                TotalIssues: issues.Count,
                InfoCount: 0,
                WarningCount: issues.Count,
                CriticalCount: 0,
                PerRuleCount: new Dictionary<SchemaRuleCode, int> { [SchemaRuleCode.MISSING_FK] = issues.Count },
                PerTableCount: new Dictionary<string, int> { ["public.orders"] = issues.Count }
            )
        );
    }

    private static SchemaAnalysisProfile CreateProfile(
        int maxIssues = 5000,
        int maxSuggestionsPerIssue = 3
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
            MaxIssues: maxIssues,
            MaxSuggestionsPerIssue: maxSuggestionsPerIssue,
            NamingConvention: NamingConvention.SnakeCase,
            NormalizationStrictness: NormalizationStrictness.Balanced,
            RequiredCommentTargets: ["Table"],
            LowQualityNameDenylist: ["tmp"],
            NameAllowlist: [],
            SynonymGroups: [new List<string> { "person", "pessoa" }],
            SemiStructuredPayloadAllowlist: [],
            DebugDiagnostics: false,
            RuleSettings: new Dictionary<SchemaRuleCode, SchemaRuleSetting>
            {
                [SchemaRuleCode.MISSING_FK] = new SchemaRuleSetting(true, 0.65, 1000),
            },
            CacheTtlSeconds: 300
        );

    private static SchemaIssue CreateIssue(
        string issueId = "issue-1",
        double confidence = 0.8500,
        SchemaTargetType targetType = SchemaTargetType.Column,
        string? tableName = "orders",
        string? columnName = "customer_id",
        string? constraintName = null,
        IReadOnlyList<SchemaEvidence>? evidence = null,
        IReadOnlyList<SchemaSuggestion>? suggestions = null
    ) =>
        new(
            IssueId: issueId,
            RuleCode: SchemaRuleCode.MISSING_FK,
            Severity: SchemaIssueSeverity.Warning,
            Confidence: confidence,
            TargetType: targetType,
            SchemaName: "public",
            TableName: tableName,
            ColumnName: columnName,
            ConstraintName: constraintName,
            Title: "Missing FK",
            Message: "FK inferred",
            Evidence: evidence ?? [new SchemaEvidence(EvidenceKind.MetadataFact, "key", "value", 1.0)],
            Suggestions: suggestions ?? [CreateSuggestion("s-1")],
            IsAmbiguous: false
        );

    private static SchemaSuggestion CreateSuggestion(
        string suggestionId,
        IReadOnlyList<SqlFixCandidate>? candidates = null
    ) =>
        new(
            SuggestionId: suggestionId,
            Title: "Suggestion",
            Description: "Description",
            Confidence: 0.9000,
            SqlCandidates: candidates ?? [CreateCandidate()]
        );

    private static SqlFixCandidate CreateCandidate(
        SqlCandidateSafety safety = SqlCandidateSafety.NonDestructive,
        CandidateVisibility visibility = CandidateVisibility.VisibleActionable,
        bool isAutoApplicable = false,
        IReadOnlyList<string>? preconditionsSql = null
    ) =>
        new(
            CandidateId: "cand-1",
            Provider: DatabaseProvider.Postgres,
            Title: "Candidate",
            Sql: "SELECT 1;",
            PreconditionsSql: preconditionsSql ?? ["SELECT 1"],
            Safety: safety,
            Visibility: visibility,
            IsAutoApplicable: isAutoApplicable,
            Notes: ["note"]
        );
}
