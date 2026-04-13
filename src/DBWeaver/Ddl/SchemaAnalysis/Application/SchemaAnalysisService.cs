using System.Security.Cryptography;
using System.Text;
using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Application.Caching;
using DBWeaver.Ddl.SchemaAnalysis.Application.Indexing;
using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;
using DBWeaver.Ddl.SchemaAnalysis.Application.Rules;
using DBWeaver.Ddl.SchemaAnalysis.Application.Validation;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Validation;
using DBWeaver.Ddl.SchemaAnalysis.Infrastructure.Hashing;
using DBWeaver.Metadata;

namespace DBWeaver.Ddl.SchemaAnalysis.Application;

public sealed class SchemaAnalysisService
{
    public const int SpecVersion = 1;

    private static readonly SchemaRuleCode[] OrderedRuleCodes =
    [
        SchemaRuleCode.FK_CATALOG_INCONSISTENT,
        SchemaRuleCode.MISSING_FK,
        SchemaRuleCode.NAMING_CONVENTION_VIOLATION,
        SchemaRuleCode.LOW_SEMANTIC_NAME,
        SchemaRuleCode.MISSING_REQUIRED_COMMENT,
        SchemaRuleCode.NF1_HINT_MULTI_VALUED,
        SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY,
        SchemaRuleCode.NF3_HINT_TRANSITIVE_DEPENDENCY,
    ];

    private readonly SchemaAnalysisProfileNormalizer _profileNormalizer;
    private readonly SchemaMetadataValidator _metadataValidator;
    private readonly SchemaMetadataIndexBuilder _indexBuilder;
    private readonly SchemaAnalysisHashingService _hashingService;
    private readonly SchemaAnalysisContractValidator _contractValidator;
    private readonly IReadOnlyDictionary<SchemaRuleCode, ISchemaAnalysisRule> _rules;
    private readonly SchemaIssueDeduplicator _issueDeduplicator;
    private readonly SchemaIssueOrderer _issueOrderer;
    private readonly SchemaSuggestionFactory _suggestionFactory;
    private readonly ISchemaAnalysisCache? _cache;

    public SchemaAnalysisService(
        IEnumerable<ISchemaAnalysisRule> rules,
        SchemaAnalysisProfileNormalizer? profileNormalizer = null,
        SchemaMetadataValidator? metadataValidator = null,
        SchemaMetadataIndexBuilder? indexBuilder = null,
        SchemaAnalysisHashingService? hashingService = null,
        SchemaAnalysisContractValidator? contractValidator = null,
        SchemaIssueDeduplicator? issueDeduplicator = null,
        SchemaIssueOrderer? issueOrderer = null,
        SchemaSuggestionFactory? suggestionFactory = null,
        ISchemaAnalysisCache? cache = null
    )
    {
        _profileNormalizer = profileNormalizer ?? new SchemaAnalysisProfileNormalizer();
        _metadataValidator = metadataValidator ?? new SchemaMetadataValidator();
        _indexBuilder = indexBuilder ?? new SchemaMetadataIndexBuilder();
        _hashingService = hashingService ?? new SchemaAnalysisHashingService();
        _contractValidator = contractValidator ?? new SchemaAnalysisContractValidator();
        _issueDeduplicator = issueDeduplicator ?? new SchemaIssueDeduplicator();
        _issueOrderer = issueOrderer ?? new SchemaIssueOrderer();
        _suggestionFactory = suggestionFactory ?? new SchemaSuggestionFactory();
        _cache = cache;
        _rules = rules.ToDictionary(static rule => rule.RuleCode);
    }

    public async Task<SchemaAnalysisResult> AnalyzeAsync(
        DbMetadata metadata,
        SchemaAnalysisProfile? rawProfile,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(metadata);

        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;

        SchemaAnalysisProfileNormalizationResult profileNormalization = _profileNormalizer.Normalize(rawProfile);
        SchemaAnalysisProfile profile = profileNormalization.Profile;

        SchemaMetadataValidationResult metadataValidation = _metadataValidator.Validate(metadata);
        if (!metadataValidation.IsValid)
        {
            return BuildFailedResult(
                metadata,
                profile,
                startedAtUtc,
                diagnostics: profileNormalization.Diagnostics.Concat(metadataValidation.Diagnostics).ToList()
            );
        }

        string metadataFingerprint = _hashingService.ComputeMetadataFingerprint(metadata);
        string profileContentHash = _hashingService.ComputeProfileContentHash(profile);

        SchemaAnalysisCacheKey cacheKey = new(
            metadataFingerprint,
            profileContentHash,
            metadata.Provider,
            SpecVersion
        );

        if (_cache is not null && profile.CacheTtlSeconds > 0 && _cache.TryGet(cacheKey, out SchemaAnalysisResult? cachedResult))
        {
            return MaterializeCacheHit(cachedResult!, startedAtUtc);
        }

        SchemaMetadataIndexSnapshot indices = _indexBuilder.Build(metadata, profile);
        SchemaAnalysisExecutionContext context = new(
            metadata,
            profile,
            indices,
            metadataFingerprint,
            profileContentHash
        );

        List<SchemaIssue> issues = [];
        List<SchemaRuleExecutionDiagnostic> diagnostics =
        [
            .. profileNormalization.Diagnostics,
            .. metadataValidation.Diagnostics,
        ];
        List<SchemaIssue> materializedIssues = [];
        int completedRules = 0;
        bool timedOut = false;
        bool cancelled = false;
        bool ruleFailurePartial = false;

        using CancellationTokenSource timeoutCts = new(profile.TimeoutMs);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token
        );

        foreach (SchemaRuleCode ruleCode in OrderedRuleCodes)
        {
            try
            {
                linkedCts.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                timedOut = true;
                diagnostics.Add(
                    new SchemaRuleExecutionDiagnostic(
                        Code: "ANL-TIMEOUT",
                        Message: "A execução atingiu o timeout global configurado.",
                        RuleCode: null,
                        State: RuleExecutionState.TimedOut,
                        IsFatal: false
                    )
                );
                break;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                break;
            }

            SchemaRuleSetting setting = profile.RuleSettings[ruleCode];
            if (!setting.Enabled)
            {
                continue;
            }

            if (!_rules.TryGetValue(ruleCode, out ISchemaAnalysisRule? rule))
            {
                continue;
            }

            SchemaRuleExecutionResult executionResult;
            try
            {
                executionResult = await rule.ExecuteAsync(context, linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                timedOut = true;
                diagnostics.Add(
                    new SchemaRuleExecutionDiagnostic(
                        Code: "ANL-TIMEOUT",
                        Message: "A execução atingiu o timeout global configurado.",
                        RuleCode: null,
                        State: RuleExecutionState.TimedOut,
                        IsFatal: false
                    )
                );
                break;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                break;
            }
            catch (Exception) when (profile.AllowPartialOnRuleFailure)
            {
                ruleFailurePartial = true;
                diagnostics.Add(
                    new SchemaRuleExecutionDiagnostic(
                        Code: "ANL-RULE-FAILED",
                        Message: "Uma regra falhou e a execução seguiu conforme política de parcial.",
                        RuleCode: ruleCode,
                        State: RuleExecutionState.Failed,
                        IsFatal: false
                    )
                );
                continue;
            }
            catch (Exception)
            {
                diagnostics.Add(
                    new SchemaRuleExecutionDiagnostic(
                        Code: "ANL-RULE-FAILED",
                        Message: "Uma regra falhou e a execução seguiu conforme política de parcial.",
                        RuleCode: ruleCode,
                        State: RuleExecutionState.Failed,
                        IsFatal: true
                    )
                );

                return BuildFailedResult(
                    metadata,
                    profile,
                    startedAtUtc,
                    diagnostics: diagnostics
                );
            }

            IReadOnlyList<SchemaIssue> orderedRuleIssues = executionResult.Issues
                .OrderByDescending(static issue => issue.Confidence)
                .ThenBy(static issue => issue.IssueId, StringComparer.Ordinal)
                .Take(setting.MaxIssues)
                .ToList();

            IReadOnlyList<SchemaIssue> thresholdFilteredIssues = orderedRuleIssues
                .Where(issue => issue.Confidence >= Math.Max(profile.MinConfidenceGlobal, setting.MinConfidence))
                .ToList();

            issues.AddRange(thresholdFilteredIssues);
            materializedIssues.AddRange(thresholdFilteredIssues);
            diagnostics.AddRange(executionResult.Diagnostics);
            completedRules++;
        }

        IReadOnlyList<SchemaIssue> dedupedIssues = _issueDeduplicator.Deduplicate(issues);
        IReadOnlyList<SchemaIssue> truncatedIssues = timedOut && !profile.AllowPartialOnTimeout
            ? []
            : dedupedIssues.Take(profile.MaxIssues).ToList();
        IReadOnlyList<SchemaIssue> orderedIssues = _issueOrderer.Order(truncatedIssues);
        IReadOnlyList<SchemaIssue> finalIssues = orderedIssues
            .Select(issue => issue with
            {
                Suggestions = _suggestionFactory.CreateSuggestions(issue, metadata.Provider, profile),
            })
            .ToList();

        SchemaAnalysisSummary summary = BuildSummary(finalIssues);
        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
        SchemaAnalysisPartialState partialState = BuildPartialState(
            timedOut,
            cancelled,
            ruleFailurePartial,
            completedRules
        );
        SchemaAnalysisStatus status = DetermineStatus(
            diagnostics,
            partialState,
            hasMaterializedIssues: materializedIssues.Count > 0,
            allowPartialOnTimeout: profile.AllowPartialOnTimeout
        );

        SchemaAnalysisResult result = new(
            AnalysisId: Guid.NewGuid().ToString("N"),
            Status: status,
            Provider: metadata.Provider,
            DatabaseName: metadata.DatabaseName,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc,
            DurationMs: Math.Max(0, (long)(completedAtUtc - startedAtUtc).TotalMilliseconds),
            MetadataFingerprint: metadataFingerprint,
            ProfileContentHash: profileContentHash,
            ProfileVersion: profile.Version,
            PartialState: partialState,
            Issues: finalIssues,
            Diagnostics: OrderDiagnostics(diagnostics),
            Summary: summary
        );

        _contractValidator.EnsureValid(result, profile);

        if (_cache is not null && profile.CacheTtlSeconds > 0)
        {
            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(profile.CacheTtlSeconds));
        }

        return result;
    }

    private static SchemaAnalysisResult BuildFailedResult(
        DbMetadata metadata,
        SchemaAnalysisProfile profile,
        DateTimeOffset startedAtUtc,
        IReadOnlyList<SchemaRuleExecutionDiagnostic> diagnostics
    )
    {
        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
        return new SchemaAnalysisResult(
            AnalysisId: Guid.NewGuid().ToString("N"),
            Status: SchemaAnalysisStatus.Failed,
            Provider: metadata.Provider,
            DatabaseName: metadata.DatabaseName,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc,
            DurationMs: Math.Max(0, (long)(completedAtUtc - startedAtUtc).TotalMilliseconds),
            MetadataFingerprint: string.Empty,
            ProfileContentHash: string.Empty,
            ProfileVersion: profile.Version,
            PartialState: new SchemaAnalysisPartialState(false, "NONE", 0, OrderedRuleCodes.Length),
            Issues: [],
            Diagnostics: OrderDiagnostics(diagnostics),
            Summary: BuildSummary([])
        );
    }

    private static SchemaAnalysisResult MaterializeCacheHit(
        SchemaAnalysisResult cachedResult,
        DateTimeOffset startedAtUtc
    )
    {
        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
        List<SchemaRuleExecutionDiagnostic> diagnostics =
        [
            .. cachedResult.Diagnostics,
            new SchemaRuleExecutionDiagnostic(
                Code: "ANL-CACHE-HIT",
                Message: "Resultado retornado do cache válido.",
                RuleCode: null,
                State: RuleExecutionState.Completed,
                IsFatal: false
            ),
        ];

        return cachedResult with
        {
            AnalysisId = Guid.NewGuid().ToString("N"),
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            DurationMs = Math.Max(0, (long)(completedAtUtc - startedAtUtc).TotalMilliseconds),
            Diagnostics = OrderDiagnostics(diagnostics),
        };
    }

    private static SchemaAnalysisSummary BuildSummary(IReadOnlyList<SchemaIssue> issues)
    {
        Dictionary<SchemaRuleCode, int> perRule = issues
            .GroupBy(static issue => issue.RuleCode)
            .ToDictionary(static group => group.Key, static group => group.Count());

        Dictionary<string, int> perTable = issues
            .Where(static issue => !string.IsNullOrWhiteSpace(issue.TableName))
            .GroupBy(static issue => string.IsNullOrWhiteSpace(issue.SchemaName) ? issue.TableName! : $"{issue.SchemaName}.{issue.TableName}")
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        return new SchemaAnalysisSummary(
            TotalIssues: issues.Count,
            InfoCount: issues.Count(static issue => issue.Severity == SchemaIssueSeverity.Info),
            WarningCount: issues.Count(static issue => issue.Severity == SchemaIssueSeverity.Warning),
            CriticalCount: issues.Count(static issue => issue.Severity == SchemaIssueSeverity.Critical),
            PerRuleCount: perRule,
            PerTableCount: perTable
        );
    }

    private static SchemaAnalysisPartialState BuildPartialState(
        bool timedOut,
        bool cancelled,
        bool ruleFailurePartial,
        int completedRules
    )
    {
        if (timedOut)
        {
            return new SchemaAnalysisPartialState(true, "TIMEOUT", completedRules, OrderedRuleCodes.Length);
        }

        if (cancelled)
        {
            return new SchemaAnalysisPartialState(true, "CANCELLED", completedRules, OrderedRuleCodes.Length);
        }

        if (ruleFailurePartial)
        {
            return new SchemaAnalysisPartialState(true, "RULE_FAILURE", completedRules, OrderedRuleCodes.Length);
        }

        return new SchemaAnalysisPartialState(false, "NONE", completedRules, OrderedRuleCodes.Length);
    }

    private static SchemaAnalysisStatus DetermineStatus(
        IReadOnlyList<SchemaRuleExecutionDiagnostic> diagnostics,
        SchemaAnalysisPartialState partialState,
        bool hasMaterializedIssues,
        bool allowPartialOnTimeout
    )
    {
        if (diagnostics.Any(static diagnostic => diagnostic.IsFatal))
        {
            return SchemaAnalysisStatus.Failed;
        }

        if (partialState.IsPartial && partialState.ReasonCode == "TIMEOUT")
        {
            return allowPartialOnTimeout ? SchemaAnalysisStatus.Partial : SchemaAnalysisStatus.Failed;
        }

        if (partialState.IsPartial && partialState.ReasonCode == "RULE_FAILURE")
        {
            return SchemaAnalysisStatus.Partial;
        }

        if (partialState.IsPartial && partialState.ReasonCode == "CANCELLED")
        {
            return hasMaterializedIssues ? SchemaAnalysisStatus.Partial : SchemaAnalysisStatus.Cancelled;
        }

        if (diagnostics.Any(static diagnostic =>
                diagnostic.Code == "ANL-METADATA-PARTIAL"
                || diagnostic.Code == "ANL-RULE-FAILED"
                || diagnostic.Code == "ANL-RULE-MAX-ISSUES-TRUNCATED"
                || diagnostic.Code == "ANL-GLOBAL-MAX-ISSUES-TRUNCATED"))
        {
            return SchemaAnalysisStatus.CompletedWithWarnings;
        }

        return SchemaAnalysisStatus.Completed;
    }

    private static IReadOnlyList<SchemaRuleExecutionDiagnostic> OrderDiagnostics(
        IEnumerable<SchemaRuleExecutionDiagnostic> diagnostics
    )
    {
        return diagnostics
            .OrderByDescending(static diagnostic => diagnostic.IsFatal)
            .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.RuleCode.HasValue ? 0 : 1)
            .ThenBy(static diagnostic => diagnostic.RuleCode)
            .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToList();
    }

}
