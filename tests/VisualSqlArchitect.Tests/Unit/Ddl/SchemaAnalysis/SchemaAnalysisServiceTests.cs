using DBWeaver.Ddl.SchemaAnalysis.Application;
using DBWeaver.Ddl.SchemaAnalysis.Application.Caching;
using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;
using DBWeaver.Ddl.SchemaAnalysis.Application.Rules;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Metadata;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_ProducesValidEmptyResult_WhenRulesReturnNoIssues()
    {
        SchemaAnalysisService service = new(
            rules: [new StubRule(SchemaRuleCode.MISSING_FK, [], [])],
            cache: new InMemorySchemaAnalysisCache()
        );

        SchemaAnalysisResult result = await service.AnalyzeAsync(CreateMetadata(), CreateProfile());

        Assert.Equal(SchemaAnalysisStatus.Completed, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(0, result.Summary.TotalIssues);
        Assert.NotEmpty(result.MetadataFingerprint);
        Assert.NotEmpty(result.ProfileContentHash);
    }

    [Fact]
    public async Task AnalyzeAsync_ExecutesActiveRule_AndReturnsIssue()
    {
        SchemaIssue issue = CreateIssue(
            issueId: "issue-1",
            ruleCode: SchemaRuleCode.MISSING_FK,
            confidence: 0.90
        );
        SchemaAnalysisService service = new(
            rules: [new StubRule(SchemaRuleCode.MISSING_FK, [issue], [])]
        );

        SchemaAnalysisResult result = await service.AnalyzeAsync(CreateMetadata(), CreateProfile());

        Assert.Single(result.Issues);
        Assert.Equal("issue-1", result.Issues[0].IssueId);
        Assert.Equal(1, result.Summary.TotalIssues);
        Assert.NotEmpty(result.Issues[0].Suggestions);
        Assert.NotEmpty(result.Issues[0].Suggestions[0].SqlCandidates);
    }

    [Fact]
    public async Task AnalyzeAsync_AttachesEmptySuggestionList_WhenFactoryHasNoMapping()
    {
        SchemaIssue issue = CreateIssue(
            issueId: "issue-1",
            ruleCode: SchemaRuleCode.FK_CATALOG_INCONSISTENT,
            confidence: 0.90
        ) with
        {
            TargetType = SchemaTargetType.Constraint,
            ConstraintName = "fk_orders_customer",
            ColumnName = null,
        };
        SchemaAnalysisService service = new(
            rules: [new StubRule(SchemaRuleCode.FK_CATALOG_INCONSISTENT, [issue], [])]
        );

        SchemaAnalysisResult result = await service.AnalyzeAsync(CreateMetadata(), CreateProfile());

        Assert.Single(result.Issues);
        Assert.Empty(result.Issues[0].Suggestions);
    }

    [Fact]
    public async Task AnalyzeAsync_TruncatesSuggestions_ByProfileLimit()
    {
        SchemaIssue issue = CreateIssue(
            issueId: "issue-1",
            ruleCode: SchemaRuleCode.MISSING_FK,
            confidence: 0.90
        );
        SchemaAnalysisProfile profile = CreateProfile() with
        {
            MaxSuggestionsPerIssue = 1,
        };
        SchemaAnalysisService service = new(
            rules: [new StubRule(SchemaRuleCode.MISSING_FK, [issue], [])]
        );

        SchemaAnalysisResult result = await service.AnalyzeAsync(CreateMetadata(), profile);

        SchemaSuggestion suggestion = Assert.Single(result.Issues[0].Suggestions);
        Assert.Equal("Review inferred foreign key", suggestion.Title);
    }

    [Fact]
    public async Task AnalyzeAsync_ExecutesRulesInNormativeOrder()
    {
        List<SchemaRuleCode> invokedRules = [];
        SchemaAnalysisService service = new(
            rules:
            [
                new TrackingRule(SchemaRuleCode.LOW_SEMANTIC_NAME, invokedRules),
                new TrackingRule(SchemaRuleCode.FK_CATALOG_INCONSISTENT, invokedRules),
                new TrackingRule(SchemaRuleCode.MISSING_FK, invokedRules),
            ]
        );

        SchemaAnalysisProfile serialProfile = CreateProfile() with
        {
            EnableParallelRules = false,
            MaxDegreeOfParallelism = 1,
        };

        await service.AnalyzeAsync(CreateMetadata(), serialProfile);

        Assert.Equal(
            [SchemaRuleCode.FK_CATALOG_INCONSISTENT, SchemaRuleCode.MISSING_FK, SchemaRuleCode.LOW_SEMANTIC_NAME],
            invokedRules
        );
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsPartial_WhenTimeoutOccursAndPartialIsAllowed()
    {
        SchemaAnalysisService service = new(
            rules: [new DelayedRule(SchemaRuleCode.MISSING_FK, TimeSpan.FromMilliseconds(80), [CreateIssue("issue-timeout", SchemaRuleCode.MISSING_FK, 0.90)])]
        );
        SchemaAnalysisProfile profile = CreateProfile() with
        {
            TimeoutMs = 10,
            AllowPartialOnTimeout = true,
        };

        SchemaAnalysisResult result = await service.AnalyzeAsync(CreateMetadata(), profile);

        Assert.Equal(SchemaAnalysisStatus.Partial, result.Status);
        Assert.Equal("TIMEOUT", result.PartialState.ReasonCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ANL-TIMEOUT");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsFailedWithoutIssues_WhenTimeoutOccursAndPartialIsNotAllowed()
    {
        SchemaAnalysisService service = new(
            rules: [new DelayedRule(SchemaRuleCode.MISSING_FK, TimeSpan.FromMilliseconds(80), [CreateIssue("issue-timeout", SchemaRuleCode.MISSING_FK, 0.90)])]
        );
        SchemaAnalysisProfile profile = CreateProfile() with
        {
            TimeoutMs = 10,
            AllowPartialOnTimeout = false,
        };

        SchemaAnalysisResult result = await service.AnalyzeAsync(CreateMetadata(), profile);

        Assert.Equal(SchemaAnalysisStatus.Failed, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal("TIMEOUT", result.PartialState.ReasonCode);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsPartial_WhenCancellationOccursAfterIssuesWereMaterialized()
    {
        SchemaAnalysisService service = new(
            rules:
            [
                new StubRule(SchemaRuleCode.FK_CATALOG_INCONSISTENT, [CreateIssue("issue-1", SchemaRuleCode.FK_CATALOG_INCONSISTENT, 0.90)], []),
                new CancellationRule(SchemaRuleCode.MISSING_FK),
            ]
        );

        SchemaAnalysisResult result = await service.AnalyzeAsync(CreateMetadata(), CreateProfile());

        Assert.Equal(SchemaAnalysisStatus.Partial, result.Status);
        Assert.Equal("CANCELLED", result.PartialState.ReasonCode);
        Assert.Single(result.Issues);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsCancelled_WhenCancellationOccursBeforeMaterialization()
    {
        SchemaAnalysisService service = new(
            rules: [new CancellationRule(SchemaRuleCode.FK_CATALOG_INCONSISTENT)]
        );

        SchemaAnalysisResult result = await service.AnalyzeAsync(CreateMetadata(), CreateProfile());

        Assert.Equal(SchemaAnalysisStatus.Cancelled, result.Status);
        Assert.Equal("CANCELLED", result.PartialState.ReasonCode);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task AnalyzeAsync_ProducesEquivalentLogicalResult_InSerialAndParallelModes()
    {
        DbMetadata metadata = CreateMetadata();
        SchemaAnalysisService serialService = new(
            rules:
            [
                new DelayedIssueRule(SchemaRuleCode.FK_CATALOG_INCONSISTENT, TimeSpan.FromMilliseconds(20), [CreateIssue("issue-a", SchemaRuleCode.FK_CATALOG_INCONSISTENT, 0.92)]),
                new DelayedIssueRule(SchemaRuleCode.MISSING_FK, TimeSpan.FromMilliseconds(5), [CreateIssue("issue-b", SchemaRuleCode.MISSING_FK, 0.88)]),
                new DelayedIssueRule(SchemaRuleCode.LOW_SEMANTIC_NAME, TimeSpan.FromMilliseconds(10), [CreateIssue("issue-c", SchemaRuleCode.LOW_SEMANTIC_NAME, 0.81)]),
            ]
        );
        SchemaAnalysisService parallelService = new(
            rules:
            [
                new DelayedIssueRule(SchemaRuleCode.FK_CATALOG_INCONSISTENT, TimeSpan.FromMilliseconds(20), [CreateIssue("issue-a", SchemaRuleCode.FK_CATALOG_INCONSISTENT, 0.92)]),
                new DelayedIssueRule(SchemaRuleCode.MISSING_FK, TimeSpan.FromMilliseconds(5), [CreateIssue("issue-b", SchemaRuleCode.MISSING_FK, 0.88)]),
                new DelayedIssueRule(SchemaRuleCode.LOW_SEMANTIC_NAME, TimeSpan.FromMilliseconds(10), [CreateIssue("issue-c", SchemaRuleCode.LOW_SEMANTIC_NAME, 0.81)]),
            ]
        );
        SchemaAnalysisProfile serialProfile = CreateProfile() with
        {
            EnableParallelRules = false,
            MaxDegreeOfParallelism = 1,
        };
        SchemaAnalysisProfile parallelProfile = CreateProfile() with
        {
            EnableParallelRules = true,
            MaxDegreeOfParallelism = 4,
        };

        SchemaAnalysisResult serialResult = await serialService.AnalyzeAsync(metadata, serialProfile);
        SchemaAnalysisResult parallelResult = await parallelService.AnalyzeAsync(metadata, parallelProfile);

        AssertEquivalentLogicalResult(serialResult, parallelResult);
    }

    [Fact]
    public async Task AnalyzeAsync_ProducesSerialEquivalentResult_WhenParallelEnabledWithDegreeOne()
    {
        DbMetadata metadata = CreateMetadata();
        SchemaAnalysisService service = new(
            rules:
            [
                new DelayedIssueRule(SchemaRuleCode.FK_CATALOG_INCONSISTENT, TimeSpan.FromMilliseconds(15), [CreateIssue("issue-a", SchemaRuleCode.FK_CATALOG_INCONSISTENT, 0.92)]),
                new DelayedIssueRule(SchemaRuleCode.MISSING_FK, TimeSpan.FromMilliseconds(5), [CreateIssue("issue-b", SchemaRuleCode.MISSING_FK, 0.88)]),
            ]
        );
        SchemaAnalysisProfile serialProfile = CreateProfile() with
        {
            EnableParallelRules = false,
            MaxDegreeOfParallelism = 1,
        };
        SchemaAnalysisProfile singleDegreeParallelProfile = CreateProfile() with
        {
            EnableParallelRules = true,
            MaxDegreeOfParallelism = 1,
        };

        SchemaAnalysisResult serialResult = await service.AnalyzeAsync(metadata, serialProfile);
        SchemaAnalysisResult singleDegreeResult = await service.AnalyzeAsync(metadata, singleDegreeParallelProfile);

        AssertEquivalentLogicalResult(serialResult, singleDegreeResult);
    }

    [Fact]
    public async Task AnalyzeAsync_ProducesStableLogicalResult_AcrossRepeatedParallelRuns()
    {
        DbMetadata metadata = CreateMetadata();
        SchemaAnalysisService service = new(
            rules:
            [
                new DelayedIssueRule(SchemaRuleCode.FK_CATALOG_INCONSISTENT, TimeSpan.FromMilliseconds(20), [CreateIssue("issue-a", SchemaRuleCode.FK_CATALOG_INCONSISTENT, 0.92)]),
                new DelayedIssueRule(SchemaRuleCode.MISSING_FK, TimeSpan.FromMilliseconds(5), [CreateIssue("issue-b", SchemaRuleCode.MISSING_FK, 0.88)]),
                new DelayedIssueRule(SchemaRuleCode.LOW_SEMANTIC_NAME, TimeSpan.FromMilliseconds(10), [CreateIssue("issue-c", SchemaRuleCode.LOW_SEMANTIC_NAME, 0.81)]),
            ]
        );
        SchemaAnalysisProfile parallelProfile = CreateProfile() with
        {
            EnableParallelRules = true,
            MaxDegreeOfParallelism = 4,
        };

        SchemaAnalysisResult baseline = await service.AnalyzeAsync(metadata, parallelProfile);

        for (int index = 0; index < 3; index++)
        {
            SchemaAnalysisResult repeated = await service.AnalyzeAsync(metadata, parallelProfile);
            AssertEquivalentLogicalResult(baseline, repeated);
        }
    }

    private static DbMetadata CreateMetadata()
    {
        TableMetadata orders = new(
            Schema: "public",
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 10,
            Columns:
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: "Order id"),
                new ColumnMetadata("customer_id", "integer", "integer", false, false, false, false, true, 2, Comment: "Customer"),
            ],
            Indexes:
            [
                new IndexMetadata("ix_orders_customer_id", false, false, false, ["customer_id"]),
            ],
            OutboundForeignKeys: [],
            InboundForeignKeys: [],
            Comment: "Orders"
        );

        return new DbMetadata(
            DatabaseName: "db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", [orders])],
            AllForeignKeys: []
        );
    }

    private static SchemaAnalysisProfile CreateProfile() =>
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
            LowQualityNameDenylist: ["tmp"],
            NameAllowlist: [],
            SynonymGroups: [["person", "pessoa"]],
            SemiStructuredPayloadAllowlist: [],
            DebugDiagnostics: false,
            RuleSettings: Enum.GetValues<SchemaRuleCode>().ToDictionary(
                static code => code,
                static code => new SchemaRuleSetting(true, code == SchemaRuleCode.FK_CATALOG_INCONSISTENT ? 0.75 : 0.55, 1000)
            ),
            CacheTtlSeconds: 300
        );

    private static SchemaIssue CreateIssue(string issueId, SchemaRuleCode ruleCode, double confidence) =>
        new(
            IssueId: issueId,
            RuleCode: ruleCode,
            Severity: SchemaIssueSeverity.Warning,
            Confidence: confidence,
            TargetType: SchemaTargetType.Column,
            SchemaName: "public",
            TableName: "orders",
            ColumnName: "customer_id",
            ConstraintName: null,
            Title: "Issue",
            Message: "Issue message",
            Evidence: ruleCode == SchemaRuleCode.MISSING_FK
                ? [
                    SchemaEvidenceFactory.MetadataFact("targetSchema", "public", 0.9),
                    SchemaEvidenceFactory.MetadataFact("targetTable", "customers", 0.9),
                    SchemaEvidenceFactory.MetadataFact("targetColumn", "id", 0.9),
                ]
                : [SchemaEvidenceFactory.MetadataFact("key", "value", 1.0)],
            Suggestions: [],
            IsAmbiguous: false
        );

    private static void AssertEquivalentLogicalResult(
        SchemaAnalysisResult expected,
        SchemaAnalysisResult actual
    )
    {
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Provider, actual.Provider);
        Assert.Equal(expected.DatabaseName, actual.DatabaseName);
        Assert.Equal(expected.ProfileVersion, actual.ProfileVersion);
        Assert.Equal(expected.PartialState, actual.PartialState);
        AssertEquivalentSummary(expected.Summary, actual.Summary);
        Assert.Equal(
            expected.Issues.Select(CreateIssueSnapshot),
            actual.Issues.Select(CreateIssueSnapshot)
        );
        Assert.Equal(
            expected.Diagnostics.Select(CreateDiagnosticSnapshot),
            actual.Diagnostics.Select(CreateDiagnosticSnapshot)
        );
    }

    private static void AssertEquivalentSummary(
        SchemaAnalysisSummary expected,
        SchemaAnalysisSummary actual
    )
    {
        Assert.Equal(expected.TotalIssues, actual.TotalIssues);
        Assert.Equal(expected.InfoCount, actual.InfoCount);
        Assert.Equal(expected.WarningCount, actual.WarningCount);
        Assert.Equal(expected.CriticalCount, actual.CriticalCount);
        Assert.Equal(
            expected.PerRuleCount.OrderBy(static entry => entry.Key),
            actual.PerRuleCount.OrderBy(static entry => entry.Key)
        );
        Assert.Equal(
            expected.PerTableCount.OrderBy(static entry => entry.Key, StringComparer.Ordinal),
            actual.PerTableCount.OrderBy(static entry => entry.Key, StringComparer.Ordinal)
        );
    }

    private static string CreateIssueSnapshot(SchemaIssue issue)
    {
        string evidence = string.Join(
            "|",
            issue.Evidence.Select(static evidence =>
                $"{evidence.Kind}:{evidence.Key}:{evidence.Value}:{evidence.Weight:F4}:{evidence.SourcePath}")
        );
        string suggestions = string.Join(
            "|",
            issue.Suggestions.Select(static suggestion =>
                $"{suggestion.SuggestionId}:{suggestion.Title}:{suggestion.Description}:{suggestion.Confidence:F4}:{string.Join("~", suggestion.SqlCandidates.Select(CreateCandidateSnapshot))}")
        );

        return string.Join(
            "||",
            issue.IssueId,
            issue.RuleCode,
            issue.Severity,
            issue.Confidence.ToString("F4"),
            issue.TargetType,
            issue.SchemaName,
            issue.TableName,
            issue.ColumnName,
            issue.ConstraintName,
            issue.Title,
            issue.Message,
            evidence,
            suggestions,
            issue.IsAmbiguous
        );
    }

    private static string CreateCandidateSnapshot(SqlFixCandidate candidate)
    {
        return string.Join(
            "::",
            candidate.CandidateId,
            candidate.Provider,
            candidate.Title,
            candidate.Sql,
            string.Join("~", candidate.PreconditionsSql),
            candidate.Safety,
            candidate.Visibility,
            candidate.IsAutoApplicable,
            string.Join("~", candidate.Notes)
        );
    }

    private static string CreateDiagnosticSnapshot(SchemaRuleExecutionDiagnostic diagnostic)
    {
        return string.Join(
            "::",
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.RuleCode,
            diagnostic.State,
            diagnostic.IsFatal
        );
    }

    private sealed class StubRule(
        SchemaRuleCode ruleCode,
        IReadOnlyList<SchemaIssue> issues,
        IReadOnlyList<SchemaRuleExecutionDiagnostic> diagnostics
    ) : ISchemaAnalysisRule
    {
        public SchemaRuleCode RuleCode => ruleCode;

        public Task<SchemaRuleExecutionResult> ExecuteAsync(
            SchemaAnalysisExecutionContext context,
            CancellationToken cancellationToken = default
        )
        {
            _ = context;
            _ = cancellationToken;
            return Task.FromResult(new SchemaRuleExecutionResult(issues, diagnostics));
        }
    }

    private sealed class TrackingRule(SchemaRuleCode ruleCode, List<SchemaRuleCode> invokedRules) : ISchemaAnalysisRule
    {
        public SchemaRuleCode RuleCode => ruleCode;

        public Task<SchemaRuleExecutionResult> ExecuteAsync(
            SchemaAnalysisExecutionContext context,
            CancellationToken cancellationToken = default
        )
        {
            _ = context;
            _ = cancellationToken;
            invokedRules.Add(ruleCode);
            return Task.FromResult(new SchemaRuleExecutionResult([], []));
        }
    }

    private sealed class DelayedRule(
        SchemaRuleCode ruleCode,
        TimeSpan delay,
        IReadOnlyList<SchemaIssue> issues
    ) : ISchemaAnalysisRule
    {
        public SchemaRuleCode RuleCode => ruleCode;

        public async Task<SchemaRuleExecutionResult> ExecuteAsync(
            SchemaAnalysisExecutionContext context,
            CancellationToken cancellationToken = default
        )
        {
            _ = context;
            await Task.Delay(delay, cancellationToken);
            return new SchemaRuleExecutionResult(issues, []);
        }
    }

    private sealed class DelayedIssueRule(
        SchemaRuleCode ruleCode,
        TimeSpan delay,
        IReadOnlyList<SchemaIssue> issues
    ) : ISchemaAnalysisRule
    {
        public SchemaRuleCode RuleCode => ruleCode;

        public async Task<SchemaRuleExecutionResult> ExecuteAsync(
            SchemaAnalysisExecutionContext context,
            CancellationToken cancellationToken = default
        )
        {
            _ = context;
            await Task.Delay(delay, cancellationToken);
            return new SchemaRuleExecutionResult(issues, []);
        }
    }

    private sealed class CancellationRule(SchemaRuleCode ruleCode) : ISchemaAnalysisRule
    {
        public SchemaRuleCode RuleCode => ruleCode;

        public Task<SchemaRuleExecutionResult> ExecuteAsync(
            SchemaAnalysisExecutionContext context,
            CancellationToken cancellationToken = default
        )
        {
            _ = context;
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
