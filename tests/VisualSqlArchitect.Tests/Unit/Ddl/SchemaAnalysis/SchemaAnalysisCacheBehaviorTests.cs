using DBWeaver.Ddl.SchemaAnalysis.Application;
using DBWeaver.Ddl.SchemaAnalysis.Application.Caching;
using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;
using DBWeaver.Ddl.SchemaAnalysis.Application.Rules;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Metadata;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaAnalysisCacheBehaviorTests
{
    [Fact]
    public async Task AnalyzeAsync_ReturnsCacheHit_OnSecondExecution()
    {
        CountingRule rule = new(SchemaRuleCode.MISSING_FK, [CreateIssue("issue-1", 0.90)]);
        SchemaAnalysisService service = new(
            rules: [rule],
            cache: new InMemorySchemaAnalysisCache()
        );

        SchemaAnalysisResult first = await service.AnalyzeAsync(CreateMetadata(), CreateProfile());
        SchemaAnalysisResult second = await service.AnalyzeAsync(CreateMetadata(), CreateProfile());

        Assert.Equal(1, rule.InvocationCount);
        Assert.Equal(first.Issues.Count, second.Issues.Count);
        Assert.Equal(first.Summary.TotalIssues, second.Summary.TotalIssues);
        Assert.Contains(second.Diagnostics, diagnostic => diagnostic.Code == "ANL-CACHE-HIT");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsCacheMiss_WhenCacheIsDisabledByTtlZero()
    {
        CountingRule rule = new(SchemaRuleCode.MISSING_FK, [CreateIssue("issue-1", 0.90)]);
        SchemaAnalysisService service = new(
            rules: [rule],
            cache: new InMemorySchemaAnalysisCache()
        );
        SchemaAnalysisProfile profile = CreateProfile() with
        {
            CacheTtlSeconds = 0,
        };

        await service.AnalyzeAsync(CreateMetadata(), profile);
        await service.AnalyzeAsync(CreateMetadata(), profile);

        Assert.Equal(2, rule.InvocationCount);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsCacheMiss_WhenProfileHashChanges()
    {
        CountingRule rule = new(SchemaRuleCode.MISSING_FK, [CreateIssue("issue-1", 0.90)]);
        SchemaAnalysisService service = new(
            rules: [rule],
            cache: new InMemorySchemaAnalysisCache()
        );

        await service.AnalyzeAsync(CreateMetadata(), CreateProfile());
        await service.AnalyzeAsync(CreateMetadata(), CreateProfile() with { MinConfidenceGlobal = 0.60 });

        Assert.Equal(2, rule.InvocationCount);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsCacheMiss_WhenMetadataFingerprintChanges()
    {
        CountingRule rule = new(SchemaRuleCode.MISSING_FK, [CreateIssue("issue-1", 0.90)]);
        SchemaAnalysisService service = new(
            rules: [rule],
            cache: new InMemorySchemaAnalysisCache()
        );

        await service.AnalyzeAsync(CreateMetadata(), CreateProfile());
        await service.AnalyzeAsync(CreateMetadata("customer_code"), CreateProfile());

        Assert.Equal(2, rule.InvocationCount);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotAccumulateTransientDiagnostics_AcrossCacheHits()
    {
        CountingRule rule = new(SchemaRuleCode.MISSING_FK, [CreateIssue("issue-1", 0.90)]);
        SchemaAnalysisService service = new(
            rules: [rule],
            cache: new InMemorySchemaAnalysisCache()
        );

        SchemaAnalysisResult first = await service.AnalyzeAsync(CreateMetadata(), CreateProfile());
        SchemaAnalysisResult second = await service.AnalyzeAsync(CreateMetadata(), CreateProfile());
        SchemaAnalysisResult third = await service.AnalyzeAsync(CreateMetadata(), CreateProfile());

        Assert.DoesNotContain(first.Diagnostics, diagnostic => diagnostic.Code == "ANL-CACHE-HIT");
        Assert.Single(second.Diagnostics, diagnostic => diagnostic.Code == "ANL-CACHE-HIT");
        Assert.Single(third.Diagnostics, diagnostic => diagnostic.Code == "ANL-CACHE-HIT");
    }

    private static DbMetadata CreateMetadata(string fkColumnName = "customer_id")
    {
        TableMetadata orders = new(
            Schema: "public",
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 10,
            Columns:
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1, Comment: "Order id"),
                new ColumnMetadata(fkColumnName, "integer", "integer", false, false, false, false, true, 2, Comment: "Customer"),
            ],
            Indexes:
            [
                new IndexMetadata($"ix_orders_{fkColumnName}", false, false, false, [fkColumnName]),
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

    private static SchemaIssue CreateIssue(string issueId, double confidence) =>
        new(
            IssueId: issueId,
            RuleCode: SchemaRuleCode.MISSING_FK,
            Severity: SchemaIssueSeverity.Warning,
            Confidence: confidence,
            TargetType: SchemaTargetType.Column,
            SchemaName: "public",
            TableName: "orders",
            ColumnName: "customer_id",
            ConstraintName: null,
            Title: "Issue",
            Message: "Issue message",
            Evidence:
            [
                SchemaEvidenceFactory.MetadataFact("targetSchema", "public", 0.9),
                SchemaEvidenceFactory.MetadataFact("targetTable", "customers", 0.9),
                SchemaEvidenceFactory.MetadataFact("targetColumn", "id", 0.9),
            ],
            Suggestions: [],
            IsAmbiguous: false
        );

    private sealed class CountingRule(
        SchemaRuleCode ruleCode,
        IReadOnlyList<SchemaIssue> issues
    ) : ISchemaAnalysisRule
    {
        public int InvocationCount { get; private set; }

        public SchemaRuleCode RuleCode => ruleCode;

        public Task<SchemaRuleExecutionResult> ExecuteAsync(
            SchemaAnalysisExecutionContext context,
            CancellationToken cancellationToken = default
        )
        {
            _ = context;
            _ = cancellationToken;
            InvocationCount++;
            return Task.FromResult(new SchemaRuleExecutionResult(issues, []));
        }
    }
}
