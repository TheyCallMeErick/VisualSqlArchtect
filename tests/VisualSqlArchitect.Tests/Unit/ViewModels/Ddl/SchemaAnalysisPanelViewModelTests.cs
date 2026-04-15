using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Ddl;

[Collection("LocalizationSensitive")]
public sealed class SchemaAnalysisPanelViewModelTests
{
    [Fact]
    public void SetLoading_FromIdle_SetsLoadingStateAndClearsMessage()
    {
        var vm = new SchemaAnalysisPanelViewModel();

        Assert.Equal(SchemaAnalysisViewState.Idle, vm.State);

        vm.SetLoading();

        Assert.Equal(SchemaAnalysisViewState.Loading, vm.State);
        Assert.Equal(string.Empty, vm.StateMessage);
    }

    [Fact]
    public void ApplyResult_WithNoIssues_SetsEmptyStateMessage()
    {
        var vm = new SchemaAnalysisPanelViewModel();

        vm.SetLoading();
        vm.ApplyResult(CreateResult([], SchemaAnalysisStatus.Completed));

        Assert.Equal(SchemaAnalysisViewState.Empty, vm.State);
        Assert.Equal(L("preview.schemaAnalysis.state.empty", "Nenhum problema estrutural inferível foi detectado."), vm.StateMessage);
        Assert.Null(vm.SelectedIssue);
        Assert.Equal(L("preview.schemaAnalysis.state.noIssueSelected", "Nenhuma issue selecionada."), vm.DetailsMessage);
        Assert.Equal(L("preview.schemaAnalysis.state.noSqlCandidate", "Nenhum SQL candidate disponível."), vm.SqlCandidatesMessage);
    }

    [Fact]
    public void ApplyResult_SelectsFirstIssue_AndFilterFallbackSelectsFirstVisible()
    {
        var vm = new SchemaAnalysisPanelViewModel();

        SchemaIssue first = CreateIssue("i-1", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "orders", 0.91d);
        SchemaIssue second = CreateIssue("i-2", SchemaRuleCode.LOW_SEMANTIC_NAME, SchemaIssueSeverity.Info, "public", "customers", 0.75d);

        vm.ApplyResult(CreateResult([first, second], SchemaAnalysisStatus.Completed));

        Assert.Equal(first, vm.SelectedIssue);

        vm.SetRuleFilter([SchemaRuleCode.LOW_SEMANTIC_NAME]);

        Assert.Equal(second, vm.SelectedIssue);
        Assert.Single(vm.VisibleIssues);
    }

    [Fact]
    public void ApplyFilters_WhenNoIssueMatches_ClearsSelectionAndShowsFilterMessage()
    {
        var vm = new SchemaAnalysisPanelViewModel();
        SchemaIssue issue = CreateIssue("i-1", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "orders", 0.91d);

        vm.ApplyResult(CreateResult([issue], SchemaAnalysisStatus.Completed));
        vm.TableTextFilter = "not-found-table";
        vm.ApplyResult(CreateResult([issue], SchemaAnalysisStatus.Completed));

        Assert.Empty(vm.VisibleIssues);
        Assert.Null(vm.SelectedIssue);
        Assert.Equal(L("preview.schemaAnalysis.state.noFilterMatch", "Nenhuma issue corresponde aos filtros selecionados."), vm.StateMessage);
    }

    [Fact]
    public void SelectedIssueEvidence_IsSortedByWeightDescThenKeyAsc()
    {
        var vm = new SchemaAnalysisPanelViewModel();
        SchemaIssue issue = CreateIssue(
            "i-1",
            SchemaRuleCode.MISSING_FK,
            SchemaIssueSeverity.Warning,
            "public",
            "orders",
            0.91d,
            [
                new SchemaEvidence(EvidenceKind.MetadataFact, "z-key", "1", 0.80d),
                new SchemaEvidence(EvidenceKind.MetadataFact, "a-key", "2", 0.80d),
                new SchemaEvidence(EvidenceKind.MetadataFact, "b-key", "3", 0.95d),
            ]
        );

        vm.ApplyResult(CreateResult([issue], SchemaAnalysisStatus.Completed));

        IReadOnlyList<SchemaEvidence> evidence = vm.SelectedIssueEvidence;
        Assert.Equal("b-key", evidence[0].Key);
        Assert.Equal("a-key", evidence[1].Key);
        Assert.Equal("z-key", evidence[2].Key);
    }

    [Fact]
    public void Commands_RespectCandidateVisibilityRules()
    {
        string? copiedSql = null;
        SqlFixCandidate? appliedCandidate = null;
        var vm = new SchemaAnalysisPanelViewModel(sql => copiedSql = sql, candidate => appliedCandidate = candidate);

        SqlFixCandidate readOnlyCandidate = CreateCandidate("c-1", CandidateVisibility.VisibleReadOnly);
        SqlFixCandidate actionableCandidate = CreateCandidate("c-2", CandidateVisibility.VisibleActionable);

        SchemaIssue issue = CreateIssue(
            "i-1",
            SchemaRuleCode.MISSING_FK,
            SchemaIssueSeverity.Warning,
            "public",
            "orders",
            0.91d,
            suggestions:
            [
                new SchemaSuggestion(
                    SuggestionId: "s-1",
                    Title: "Sugestão",
                    Description: "Desc",
                    Confidence: 0.90d,
                    SqlCandidates: [readOnlyCandidate, actionableCandidate]
                ),
            ]
        );

        vm.ApplyResult(CreateResult([issue], SchemaAnalysisStatus.Completed));

        Assert.True(vm.CanCopySql);
        Assert.False(vm.CanApplyToCanvas);
        vm.CopySqlCommand.Execute(null);
        Assert.Equal(readOnlyCandidate.Sql, copiedSql);

        vm.SelectedSqlCandidate = actionableCandidate;
        Assert.True(vm.CanApplyToCanvas);
        vm.ApplyToCanvasCommand.Execute(null);
        Assert.Equal(actionableCandidate, appliedCandidate);
    }

    [Fact]
    public void Filters_AreCumulativeAndKeepRawSummaryUntouched()
    {
        var vm = new SchemaAnalysisPanelViewModel();

        SchemaIssue warning = CreateIssue("i-1", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "orders", 0.91d);
        SchemaIssue info = CreateIssue("i-2", SchemaRuleCode.LOW_SEMANTIC_NAME, SchemaIssueSeverity.Info, "public", "customers", 0.60d);
        SchemaIssue critical = CreateIssue("i-3", SchemaRuleCode.FK_CATALOG_INCONSISTENT, SchemaIssueSeverity.Critical, "public", "payments", 0.95d);

        vm.ApplyResult(CreateResult([warning, info, critical], SchemaAnalysisStatus.Completed));

        Assert.Equal(3, vm.RawTotalIssues);

        vm.SetSeverityFilter([SchemaIssueSeverity.Warning]);
        vm.SetRuleFilter([SchemaRuleCode.MISSING_FK]);
        vm.MinConfidenceFilter = 0.80d;
        vm.TableTextFilter = "public.orders";

        Assert.Single(vm.VisibleIssues);
        Assert.Equal(3, vm.RawTotalIssues);
        Assert.Equal(1, vm.FilteredTotalIssues);
    }

    [Fact]
    public void BindableFilterFlags_ApplyCumulativeFiltering()
    {
        var vm = new SchemaAnalysisPanelViewModel();

        SchemaIssue warning = CreateIssue("i-1", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "orders", 0.91d);
        SchemaIssue info = CreateIssue("i-2", SchemaRuleCode.LOW_SEMANTIC_NAME, SchemaIssueSeverity.Info, "public", "customers", 0.60d);

        vm.ApplyResult(CreateResult([warning, info], SchemaAnalysisStatus.Completed));

        vm.IncludeInfo = false;
        vm.IncludeCritical = false;
        vm.IncludeMissingFk = true;
        vm.IncludeLowSemanticName = false;

        Assert.Single(vm.VisibleIssues);
        Assert.Equal(SchemaRuleCode.MISSING_FK, vm.VisibleIssues[0].RuleCode);
        Assert.Equal(2, vm.RawTotalIssues);
    }

    [Fact]
    public void ClearFiltersCommand_RestoresFilterFlagsAndThresholds()
    {
        var vm = new SchemaAnalysisPanelViewModel();
        SchemaIssue issue = CreateIssue("i-1", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "orders", 0.91d);

        vm.ApplyResult(CreateResult([issue], SchemaAnalysisStatus.Completed));
        vm.IncludeWarning = false;
        vm.IncludeMissingFk = false;
        vm.MinConfidenceFilter = 0.9d;
        vm.TableTextFilter = "public.orders";

        vm.ClearFiltersCommand.Execute(null);

        Assert.True(vm.IncludeInfo);
        Assert.True(vm.IncludeWarning);
        Assert.True(vm.IncludeCritical);
        Assert.True(vm.IncludeMissingFk);
        Assert.Equal(0d, vm.MinConfidenceFilter);
        Assert.Equal(string.Empty, vm.TableTextFilter);
    }

    [Fact]
    public void AddIgnoredTableCommand_FiltersVisibleIssuesByTableName()
    {
        var vm = new SchemaAnalysisPanelViewModel();

        SchemaIssue orders = CreateIssue("i-1", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "orders", 0.91d);
        SchemaIssue customers = CreateIssue("i-2", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "customers", 0.91d);

        vm.ApplyResult(CreateResult([orders, customers], SchemaAnalysisStatus.Completed));

        vm.IgnoredTableInput = "public.orders";
        vm.AddIgnoredTableCommand.Execute(null);

        Assert.Single(vm.IgnoredTables);
        Assert.Equal("public.orders", vm.IgnoredTables[0]);
        Assert.Single(vm.VisibleIssues);
        Assert.Equal("customers", vm.VisibleIssues[0].TableName);
    }

    [Fact]
    public void ShouldIgnoreTableForAnalysis_RespectsIgnoreViewsAndBlacklist()
    {
        var vm = new SchemaAnalysisPanelViewModel();

        vm.IgnoreViews = true;
        vm.IgnoredTableInput = "public.orders";
        vm.AddIgnoredTableCommand.Execute(null);

        Assert.True(vm.ShouldIgnoreTableForAnalysis("public", "v_orders", TableKind.View));
        Assert.True(vm.ShouldIgnoreTableForAnalysis("public", "orders", TableKind.Table));
        Assert.False(vm.ShouldIgnoreTableForAnalysis("public", "customers", TableKind.Table));
    }

    [Fact]
    public void SelectedIssueDiagnostics_ShowsOnlyCurrentRuleDiagnostics()
    {
        var vm = new SchemaAnalysisPanelViewModel();
        SchemaIssue issue = CreateIssue("i-1", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "orders", 0.91d);

        SchemaAnalysisResult result = CreateResult([issue], SchemaAnalysisStatus.Completed) with
        {
            Diagnostics =
            [
                new SchemaRuleExecutionDiagnostic("ANL-METADATA-PARTIAL", "partial missing metadata", SchemaRuleCode.MISSING_FK, RuleExecutionState.Completed, false),
                new SchemaRuleExecutionDiagnostic("ANL-RULE-DISABLED", "disabled", SchemaRuleCode.NF1_HINT_MULTI_VALUED, RuleExecutionState.Skipped, false),
            ],
        };

        vm.ApplyResult(result);

        IReadOnlyList<SchemaRuleExecutionDiagnostic> diagnostics = vm.SelectedIssueDiagnostics;
        Assert.Single(diagnostics);
        Assert.Equal("ANL-METADATA-PARTIAL", diagnostics[0].Code);
    }

    [Fact]
    public void SetCancelled_SetsCancelledStateAndMessage()
    {
        var vm = new SchemaAnalysisPanelViewModel();

        vm.SetCancelled();

        Assert.Equal(SchemaAnalysisViewState.Cancelled, vm.State);
        Assert.Equal(L("preview.schemaAnalysis.state.cancelled", "Análise cancelada pelo usuário."), vm.StateMessage);
    }

    [Fact]
    public void ApplyResult_WithCancelledStatus_MapsToCancelledState()
    {
        var vm = new SchemaAnalysisPanelViewModel();
        SchemaIssue issue = CreateIssue("i-1", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "orders", 0.91d);

        SchemaAnalysisResult result = CreateResult([issue], SchemaAnalysisStatus.Cancelled) with
        {
            PartialState = new SchemaAnalysisPartialState(true, "CANCELLED", 3, 8),
        };

        vm.ApplyResult(result);

        Assert.Equal(SchemaAnalysisViewState.Cancelled, vm.State);
        Assert.Equal(L("preview.schemaAnalysis.state.cancelled", "Análise cancelada pelo usuário."), vm.StateMessage);
    }

    [Fact]
    public void ApplyResult_WithPartialTimeoutReason_ShowsTimeoutMessage()
    {
        var vm = new SchemaAnalysisPanelViewModel();
        SchemaIssue issue = CreateIssue("i-1", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "orders", 0.91d);

        SchemaAnalysisResult result = CreateResult([issue], SchemaAnalysisStatus.Partial) with
        {
            PartialState = new SchemaAnalysisPartialState(true, "TIMEOUT", 5, 8),
        };

        vm.ApplyResult(result);

        Assert.Equal(SchemaAnalysisViewState.Partial, vm.State);
        Assert.Equal(L("preview.schemaAnalysis.state.partialTimeout", "Análise finalizada parcialmente por timeout."), vm.StateMessage);
    }

    [Fact]
    public void ApplyResult_WithPartialCancelledReason_ShowsCancelledMessage()
    {
        var vm = new SchemaAnalysisPanelViewModel();
        SchemaIssue issue = CreateIssue("i-1", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "orders", 0.91d);

        SchemaAnalysisResult result = CreateResult([issue], SchemaAnalysisStatus.Partial) with
        {
            PartialState = new SchemaAnalysisPartialState(true, "CANCELLED", 4, 8),
        };

        vm.ApplyResult(result);

        Assert.Equal(SchemaAnalysisViewState.Partial, vm.State);
        Assert.Equal(L("preview.schemaAnalysis.state.cancelled", "Análise cancelada pelo usuário."), vm.StateMessage);
    }

    [Fact]
    public void ApplyResult_WithFailedStatus_ShowsFailedStateMessage()
    {
        var vm = new SchemaAnalysisPanelViewModel();
        SchemaIssue issue = CreateIssue("i-1", SchemaRuleCode.MISSING_FK, SchemaIssueSeverity.Warning, "public", "orders", 0.91d);

        vm.ApplyResult(CreateResult([issue], SchemaAnalysisStatus.Failed));

        Assert.Equal(SchemaAnalysisViewState.Failed, vm.State);
        Assert.Equal(L("preview.schemaAnalysis.state.failed", "Falha na análise estrutural."), vm.StateMessage);
    }

    [Fact]
    public void ActionBlockedTooltip_WithoutEligibleCandidate_ShowsBlockedMessage()
    {
        var vm = new SchemaAnalysisPanelViewModel();
        SqlFixCandidate hiddenCandidate = CreateCandidate("c-1", CandidateVisibility.Hidden);
        SchemaIssue issue = CreateIssue(
            "i-1",
            SchemaRuleCode.MISSING_FK,
            SchemaIssueSeverity.Warning,
            "public",
            "orders",
            0.91d,
            suggestions:
            [
                new SchemaSuggestion(
                    SuggestionId: "s-1",
                    Title: "Sugestão",
                    Description: "Desc",
                    Confidence: 0.90d,
                    SqlCandidates: [hiddenCandidate]
                ),
            ]
        );

        vm.ApplyResult(CreateResult([issue], SchemaAnalysisStatus.Completed));

        Assert.False(vm.CanCopySql);
        Assert.False(vm.CanApplyToCanvas);
        Assert.Equal(L("preview.schemaAnalysis.actionBlockedTooltip", "Ação indisponível para o nível de risco ou capacidade atual."), vm.ActionBlockedTooltip);
    }

    private static SchemaAnalysisResult CreateResult(
        IReadOnlyList<SchemaIssue> issues,
        SchemaAnalysisStatus status
    )
    {
        return new SchemaAnalysisResult(
            AnalysisId: "analysis",
            Status: status,
            Provider: DatabaseProvider.Postgres,
            DatabaseName: "db",
            StartedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            DurationMs: 10,
            MetadataFingerprint: "fingerprint",
            ProfileContentHash: "profile",
            ProfileVersion: 1,
            PartialState: new SchemaAnalysisPartialState(false, "NONE", 8, 8),
            Issues: issues,
            Diagnostics: [],
            Summary: new SchemaAnalysisSummary(
                TotalIssues: issues.Count,
                InfoCount: issues.Count(i => i.Severity == SchemaIssueSeverity.Info),
                WarningCount: issues.Count(i => i.Severity == SchemaIssueSeverity.Warning),
                CriticalCount: issues.Count(i => i.Severity == SchemaIssueSeverity.Critical),
                PerRuleCount: new Dictionary<SchemaRuleCode, int>(),
                PerTableCount: new Dictionary<string, int>()
            )
        );
    }

    private static SchemaIssue CreateIssue(
        string issueId,
        SchemaRuleCode ruleCode,
        SchemaIssueSeverity severity,
        string schema,
        string table,
        double confidence,
        IReadOnlyList<SchemaEvidence>? evidence = null,
        IReadOnlyList<SchemaSuggestion>? suggestions = null
    )
    {
        return new SchemaIssue(
            IssueId: issueId,
            RuleCode: ruleCode,
            Severity: severity,
            Confidence: confidence,
            TargetType: SchemaTargetType.Column,
            SchemaName: schema,
            TableName: table,
            ColumnName: "id",
            ConstraintName: null,
            Title: "Title",
            Message: "Message",
            Evidence: evidence ?? [new SchemaEvidence(EvidenceKind.MetadataFact, "k", "v", 1.0d)],
            Suggestions: suggestions ?? [],
            IsAmbiguous: false
        );
    }

    private static SqlFixCandidate CreateCandidate(string candidateId, CandidateVisibility visibility)
    {
        return new SqlFixCandidate(
            CandidateId: candidateId,
            Provider: DatabaseProvider.Postgres,
            Title: "Candidate",
            Sql: "ALTER TABLE t ADD COLUMN x int;",
            PreconditionsSql: ["SELECT 1"],
            Safety: visibility == CandidateVisibility.VisibleActionable
                ? SqlCandidateSafety.NonDestructive
                : SqlCandidateSafety.PotentiallyDestructive,
            Visibility: visibility,
            IsAutoApplicable: false,
            Notes: []
        );
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
