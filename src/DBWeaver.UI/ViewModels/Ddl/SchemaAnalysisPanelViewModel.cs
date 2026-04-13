using System.Collections.ObjectModel;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.ViewModels;

public sealed class SchemaAnalysisPanelViewModel : ViewModelBase
{
    private const string EmptyMessageFallback = "Nenhum problema estrutural inferível foi detectado.";
    private const string MetadataUnavailableMessageFallback = "Metadata indisponível para análise estrutural.";
    private const string PartialTimeoutMessageFallback = "Análise finalizada parcialmente por timeout.";
    private const string CancelledMessageFallback = "Análise cancelada pelo usuário.";
    private const string FailedMessageFallback = "Falha na análise estrutural.";
    private const string NoFilterMatchMessageFallback = "Nenhuma issue corresponde aos filtros selecionados.";
    private const string NoIssueSelectedMessageFallback = "Nenhuma issue selecionada.";
    private const string NoSqlCandidateMessageFallback = "Nenhum SQL candidate disponível.";
    private const string ActionBlockedTooltipFallback = "Ação indisponível para o nível de risco ou capacidade atual.";

    private readonly Action<string>? _copySql;
    private readonly Action<SqlFixCandidate>? _applyToCanvas;

    private readonly List<SchemaIssue> _rawIssues = [];
    private readonly List<SchemaRuleExecutionDiagnostic> _diagnostics = [];
    private readonly HashSet<SchemaIssueSeverity> _severityFilter = [];
    private readonly HashSet<SchemaRuleCode> _ruleFilter = [];

    private SchemaAnalysisViewState _state;
    private string _stateMessage = L("preview.schemaAnalysis.state.metadataUnavailable", MetadataUnavailableMessageFallback);
    private double _minConfidenceFilter;
    private string _tableTextFilter = string.Empty;
    private SchemaIssue? _selectedIssue;
    private SchemaSuggestion? _selectedSuggestion;
    private SqlFixCandidate? _selectedSqlCandidate;
    private bool _includeInfo = true;
    private bool _includeWarning = true;
    private bool _includeCritical = true;
    private bool _includeFkCatalogInconsistent = true;
    private bool _includeMissingFk = true;
    private bool _includeNamingConventionViolation = true;
    private bool _includeLowSemanticName = true;
    private bool _includeMissingRequiredComment = true;
    private bool _includeNf1HintMultiValued = true;
    private bool _includeNf2HintPartialDependency = true;
    private bool _includeNf3HintTransitiveDependency = true;

    public SchemaAnalysisPanelViewModel(
        Action<string>? copySql = null,
        Action<SqlFixCandidate>? applyToCanvas = null
    )
    {
        _copySql = copySql;
        _applyToCanvas = applyToCanvas;

        CopySqlCommand = new RelayCommand(
            () =>
            {
                if (SelectedSqlCandidate is not null)
                    _copySql?.Invoke(SelectedSqlCandidate.Sql);
            },
            () => CanCopySql
        );

        ApplyToCanvasCommand = new RelayCommand(
            () =>
            {
                if (SelectedSqlCandidate is not null)
                    _applyToCanvas?.Invoke(SelectedSqlCandidate);
            },
            () => CanApplyToCanvas
        );

        ClearFiltersCommand = new RelayCommand(ClearFilters);
    }

    public ObservableCollection<SchemaIssue> VisibleIssues { get; } = [];

    public SchemaAnalysisViewState State
    {
        get => _state;
        private set => Set(ref _state, value);
    }

    public string StateMessage
    {
        get => _stateMessage;
        private set => Set(ref _stateMessage, value);
    }

    public IReadOnlyList<SchemaRuleExecutionDiagnostic> Diagnostics => _diagnostics;

    public SchemaIssue? SelectedIssue
    {
        get => _selectedIssue;
        set
        {
            if (!Set(ref _selectedIssue, value))
                return;

            SelectedSuggestion = value?.Suggestions.OrderByDescending(s => s.Confidence).ThenBy(s => s.Title, StringComparer.Ordinal).ThenBy(s => s.SuggestionId, StringComparer.Ordinal).FirstOrDefault();
            RaisePropertyChanged(nameof(SelectedIssueEvidence));
            RaisePropertyChanged(nameof(SelectedIssueDiagnostics));
            RaisePropertyChanged(nameof(DetailsMessage));
        }
    }

    public SchemaSuggestion? SelectedSuggestion
    {
        get => _selectedSuggestion;
        set
        {
            if (!Set(ref _selectedSuggestion, value))
                return;

            SelectedSqlCandidate = VisibleCandidates.FirstOrDefault();
            RaisePropertyChanged(nameof(VisibleCandidates));
            RaisePropertyChanged(nameof(SqlCandidatesMessage));
        }
    }

    public SqlFixCandidate? SelectedSqlCandidate
    {
        get => _selectedSqlCandidate;
        set
        {
            if (!Set(ref _selectedSqlCandidate, value))
                return;

            RaisePropertyChanged(nameof(CanCopySql));
            RaisePropertyChanged(nameof(CanApplyToCanvas));
            RaisePropertyChanged(nameof(ActionBlockedTooltip));
            CopySqlCommand.NotifyCanExecuteChanged();
            ApplyToCanvasCommand.NotifyCanExecuteChanged();
        }
    }

    public IReadOnlyList<SchemaEvidence> SelectedIssueEvidence =>
        SelectedIssue?.Evidence.OrderByDescending(e => e.Weight).ThenBy(e => e.Key, StringComparer.Ordinal).ToArray()
        ?? [];

    public IReadOnlyList<SchemaRuleExecutionDiagnostic> SelectedIssueDiagnostics =>
        SelectedIssue is null
            ? []
            : _diagnostics
                .Where(d => d.RuleCode == SelectedIssue.RuleCode)
                .OrderByDescending(d => d.IsFatal)
                .ThenBy(d => d.Code, StringComparer.Ordinal)
                .ThenBy(d => d.Message, StringComparer.Ordinal)
                .ToArray();

    public IReadOnlyList<SqlFixCandidate> VisibleCandidates =>
        SelectedSuggestion?.SqlCandidates.Where(c => c.Visibility != CandidateVisibility.Hidden).ToArray() ?? [];

    public string DetailsMessage => SelectedIssue is null
        ? L("preview.schemaAnalysis.state.noIssueSelected", NoIssueSelectedMessageFallback)
        : SelectedIssue.Message;

    public string SqlCandidatesMessage => VisibleCandidates.Count == 0
        ? L("preview.schemaAnalysis.state.noSqlCandidate", NoSqlCandidateMessageFallback)
        : string.Empty;

    public bool CanCopySql =>
        SelectedSqlCandidate is not null
        && SelectedSqlCandidate.Visibility is CandidateVisibility.VisibleReadOnly or CandidateVisibility.VisibleActionable;

    public bool CanApplyToCanvas =>
        SelectedSqlCandidate is not null
        && SelectedSqlCandidate.Visibility == CandidateVisibility.VisibleActionable;

    public string ActionBlockedTooltip =>
        CanCopySql || CanApplyToCanvas
            ? string.Empty
            : L("preview.schemaAnalysis.actionBlockedTooltip", ActionBlockedTooltipFallback);

    public RelayCommand CopySqlCommand { get; }

    public RelayCommand ApplyToCanvasCommand { get; }

    public RelayCommand ClearFiltersCommand { get; }

    public int RawTotalIssues => _rawIssues.Count;

    public int RawInfoCount => _rawIssues.Count(static i => i.Severity == SchemaIssueSeverity.Info);

    public int RawWarningCount => _rawIssues.Count(static i => i.Severity == SchemaIssueSeverity.Warning);

    public int RawCriticalCount => _rawIssues.Count(static i => i.Severity == SchemaIssueSeverity.Critical);

    public int FilteredTotalIssues => VisibleIssues.Count;

    public int FilteredInfoCount => VisibleIssues.Count(static i => i.Severity == SchemaIssueSeverity.Info);

    public int FilteredWarningCount => VisibleIssues.Count(static i => i.Severity == SchemaIssueSeverity.Warning);

    public int FilteredCriticalCount => VisibleIssues.Count(static i => i.Severity == SchemaIssueSeverity.Critical);

    public double MinConfidenceFilter
    {
        get => _minConfidenceFilter;
        set
        {
            double clamped = Math.Clamp(value, 0d, 1d);
            if (!Set(ref _minConfidenceFilter, clamped))
                return;

            ApplyFilters();
        }
    }

    public string TableTextFilter
    {
        get => _tableTextFilter;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (!Set(ref _tableTextFilter, normalized))
                return;

            ApplyFilters();
        }
    }

    public bool IncludeInfo
    {
        get => _includeInfo;
        set
        {
            if (!Set(ref _includeInfo, value))
                return;

            RebuildSeverityFilterFromFlags();
        }
    }

    public bool IncludeWarning
    {
        get => _includeWarning;
        set
        {
            if (!Set(ref _includeWarning, value))
                return;

            RebuildSeverityFilterFromFlags();
        }
    }

    public bool IncludeCritical
    {
        get => _includeCritical;
        set
        {
            if (!Set(ref _includeCritical, value))
                return;

            RebuildSeverityFilterFromFlags();
        }
    }

    public bool IncludeFkCatalogInconsistent
    {
        get => _includeFkCatalogInconsistent;
        set
        {
            if (!Set(ref _includeFkCatalogInconsistent, value))
                return;

            RebuildRuleFilterFromFlags();
        }
    }

    public bool IncludeMissingFk
    {
        get => _includeMissingFk;
        set
        {
            if (!Set(ref _includeMissingFk, value))
                return;

            RebuildRuleFilterFromFlags();
        }
    }

    public bool IncludeNamingConventionViolation
    {
        get => _includeNamingConventionViolation;
        set
        {
            if (!Set(ref _includeNamingConventionViolation, value))
                return;

            RebuildRuleFilterFromFlags();
        }
    }

    public bool IncludeLowSemanticName
    {
        get => _includeLowSemanticName;
        set
        {
            if (!Set(ref _includeLowSemanticName, value))
                return;

            RebuildRuleFilterFromFlags();
        }
    }

    public bool IncludeMissingRequiredComment
    {
        get => _includeMissingRequiredComment;
        set
        {
            if (!Set(ref _includeMissingRequiredComment, value))
                return;

            RebuildRuleFilterFromFlags();
        }
    }

    public bool IncludeNf1HintMultiValued
    {
        get => _includeNf1HintMultiValued;
        set
        {
            if (!Set(ref _includeNf1HintMultiValued, value))
                return;

            RebuildRuleFilterFromFlags();
        }
    }

    public bool IncludeNf2HintPartialDependency
    {
        get => _includeNf2HintPartialDependency;
        set
        {
            if (!Set(ref _includeNf2HintPartialDependency, value))
                return;

            RebuildRuleFilterFromFlags();
        }
    }

    public bool IncludeNf3HintTransitiveDependency
    {
        get => _includeNf3HintTransitiveDependency;
        set
        {
            if (!Set(ref _includeNf3HintTransitiveDependency, value))
                return;

            RebuildRuleFilterFromFlags();
        }
    }

    public void SetSeverityFilter(IEnumerable<SchemaIssueSeverity> severities)
    {
        _severityFilter.Clear();
        foreach (SchemaIssueSeverity severity in severities)
            _severityFilter.Add(severity);

        _includeInfo = _severityFilter.Contains(SchemaIssueSeverity.Info);
        _includeWarning = _severityFilter.Contains(SchemaIssueSeverity.Warning);
        _includeCritical = _severityFilter.Contains(SchemaIssueSeverity.Critical);
        RaisePropertyChanged(nameof(IncludeInfo));
        RaisePropertyChanged(nameof(IncludeWarning));
        RaisePropertyChanged(nameof(IncludeCritical));

        ApplyFilters();
    }

    public void SetRuleFilter(IEnumerable<SchemaRuleCode> rules)
    {
        _ruleFilter.Clear();
        foreach (SchemaRuleCode rule in rules)
            _ruleFilter.Add(rule);

        _includeFkCatalogInconsistent = _ruleFilter.Contains(SchemaRuleCode.FK_CATALOG_INCONSISTENT);
        _includeMissingFk = _ruleFilter.Contains(SchemaRuleCode.MISSING_FK);
        _includeNamingConventionViolation = _ruleFilter.Contains(SchemaRuleCode.NAMING_CONVENTION_VIOLATION);
        _includeLowSemanticName = _ruleFilter.Contains(SchemaRuleCode.LOW_SEMANTIC_NAME);
        _includeMissingRequiredComment = _ruleFilter.Contains(SchemaRuleCode.MISSING_REQUIRED_COMMENT);
        _includeNf1HintMultiValued = _ruleFilter.Contains(SchemaRuleCode.NF1_HINT_MULTI_VALUED);
        _includeNf2HintPartialDependency = _ruleFilter.Contains(SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY);
        _includeNf3HintTransitiveDependency = _ruleFilter.Contains(SchemaRuleCode.NF3_HINT_TRANSITIVE_DEPENDENCY);
        RaisePropertyChanged(nameof(IncludeFkCatalogInconsistent));
        RaisePropertyChanged(nameof(IncludeMissingFk));
        RaisePropertyChanged(nameof(IncludeNamingConventionViolation));
        RaisePropertyChanged(nameof(IncludeLowSemanticName));
        RaisePropertyChanged(nameof(IncludeMissingRequiredComment));
        RaisePropertyChanged(nameof(IncludeNf1HintMultiValued));
        RaisePropertyChanged(nameof(IncludeNf2HintPartialDependency));
        RaisePropertyChanged(nameof(IncludeNf3HintTransitiveDependency));

        ApplyFilters();
    }

    public void ClearFilters()
    {
        _includeInfo = true;
        _includeWarning = true;
        _includeCritical = true;
        _includeFkCatalogInconsistent = true;
        _includeMissingFk = true;
        _includeNamingConventionViolation = true;
        _includeLowSemanticName = true;
        _includeMissingRequiredComment = true;
        _includeNf1HintMultiValued = true;
        _includeNf2HintPartialDependency = true;
        _includeNf3HintTransitiveDependency = true;
        RaisePropertyChanged(nameof(IncludeInfo));
        RaisePropertyChanged(nameof(IncludeWarning));
        RaisePropertyChanged(nameof(IncludeCritical));
        RaisePropertyChanged(nameof(IncludeFkCatalogInconsistent));
        RaisePropertyChanged(nameof(IncludeMissingFk));
        RaisePropertyChanged(nameof(IncludeNamingConventionViolation));
        RaisePropertyChanged(nameof(IncludeLowSemanticName));
        RaisePropertyChanged(nameof(IncludeMissingRequiredComment));
        RaisePropertyChanged(nameof(IncludeNf1HintMultiValued));
        RaisePropertyChanged(nameof(IncludeNf2HintPartialDependency));
        RaisePropertyChanged(nameof(IncludeNf3HintTransitiveDependency));

        _minConfidenceFilter = 0d;
        _tableTextFilter = string.Empty;
        RaisePropertyChanged(nameof(MinConfidenceFilter));
        RaisePropertyChanged(nameof(TableTextFilter));
        RebuildSeverityFilterFromFlags(applyFilters: false);
        RebuildRuleFilterFromFlags(applyFilters: false);
        ApplyFilters();
    }

    public void SetMetadataUnavailable()
    {
        _rawIssues.Clear();
        _diagnostics.Clear();
        VisibleIssues.Clear();
        SelectedIssue = null;
        State = SchemaAnalysisViewState.Idle;
        StateMessage = L("preview.schemaAnalysis.state.metadataUnavailable", MetadataUnavailableMessageFallback);
        RaiseSummaryCountersChanged();
    }

    public void SetLoading()
    {
        State = SchemaAnalysisViewState.Loading;
        StateMessage = string.Empty;
    }

    public void SetCancelled()
    {
        State = SchemaAnalysisViewState.Cancelled;
        StateMessage = L("preview.schemaAnalysis.state.cancelled", CancelledMessageFallback);
    }

    public void ApplyResult(SchemaAnalysisResult result)
    {
        _rawIssues.Clear();
        _rawIssues.AddRange(result.Issues);

        _diagnostics.Clear();
        _diagnostics.AddRange(result.Diagnostics);
        RaisePropertyChanged(nameof(Diagnostics));

        ApplyFilters();

        (SchemaAnalysisViewState state, string message) = MapState(result.Status, result.PartialState.ReasonCode, _rawIssues.Count, VisibleIssues.Count);
        State = state;
        StateMessage = message;
    }

    private static (SchemaAnalysisViewState State, string Message) MapState(
        SchemaAnalysisStatus status,
        string reasonCode,
        int rawIssueCount,
        int filteredIssueCount
    )
    {
        if (status == SchemaAnalysisStatus.Failed)
            return (SchemaAnalysisViewState.Failed, L("preview.schemaAnalysis.state.failed", FailedMessageFallback));

        if (status == SchemaAnalysisStatus.Cancelled)
            return (SchemaAnalysisViewState.Cancelled, L("preview.schemaAnalysis.state.cancelled", CancelledMessageFallback));

        if (status == SchemaAnalysisStatus.Partial)
        {
            if (string.Equals(reasonCode, "TIMEOUT", StringComparison.Ordinal))
                return (SchemaAnalysisViewState.Partial, L("preview.schemaAnalysis.state.partialTimeout", PartialTimeoutMessageFallback));

            if (string.Equals(reasonCode, "CANCELLED", StringComparison.Ordinal))
                return (SchemaAnalysisViewState.Partial, L("preview.schemaAnalysis.state.cancelled", CancelledMessageFallback));

            return (SchemaAnalysisViewState.Partial, string.Empty);
        }

        if (rawIssueCount == 0)
            return (SchemaAnalysisViewState.Empty, L("preview.schemaAnalysis.state.empty", EmptyMessageFallback));

        if (filteredIssueCount == 0)
            return (SchemaAnalysisViewState.Completed, L("preview.schemaAnalysis.state.noFilterMatch", NoFilterMatchMessageFallback));

        return (SchemaAnalysisViewState.Completed, string.Empty);
    }

    private void ApplyFilters()
    {
        List<SchemaIssue> filtered = _rawIssues
            .Where(i => _severityFilter.Count == 0 || _severityFilter.Contains(i.Severity))
            .Where(i => _ruleFilter.Count == 0 || _ruleFilter.Contains(i.RuleCode))
            .Where(i => i.Confidence >= MinConfidenceFilter)
            .Where(i => MatchesTableFilter(i, TableTextFilter))
            .ToList();

        VisibleIssues.Clear();
        foreach (SchemaIssue issue in filtered)
            VisibleIssues.Add(issue);

        ReconcileSelection();
        RaiseSummaryCountersChanged();

        if (State is SchemaAnalysisViewState.Completed or SchemaAnalysisViewState.Empty)
        {
            (_, string message) = MapState(
                SchemaAnalysisStatus.Completed,
                "NONE",
                _rawIssues.Count,
                VisibleIssues.Count
            );
            StateMessage = message;
        }
    }

    private void ReconcileSelection()
    {
        if (VisibleIssues.Count == 0)
        {
            SelectedIssue = null;
            return;
        }

        if (SelectedIssue is null || !VisibleIssues.Contains(SelectedIssue))
        {
            SelectedIssue = VisibleIssues[0];
        }
    }

    private static bool MatchesTableFilter(SchemaIssue issue, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        string schema = issue.SchemaName ?? string.Empty;
        string table = issue.TableName ?? string.Empty;
        string value = string.IsNullOrWhiteSpace(schema) ? table : $"{schema}.{table}";

        return value.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void RaiseSummaryCountersChanged()
    {
        RaisePropertyChanged(nameof(RawTotalIssues));
        RaisePropertyChanged(nameof(RawInfoCount));
        RaisePropertyChanged(nameof(RawWarningCount));
        RaisePropertyChanged(nameof(RawCriticalCount));
        RaisePropertyChanged(nameof(FilteredTotalIssues));
        RaisePropertyChanged(nameof(FilteredInfoCount));
        RaisePropertyChanged(nameof(FilteredWarningCount));
        RaisePropertyChanged(nameof(FilteredCriticalCount));
    }

    private void RebuildSeverityFilterFromFlags(bool applyFilters = true)
    {
        _severityFilter.Clear();
        if (IncludeInfo)
            _severityFilter.Add(SchemaIssueSeverity.Info);
        if (IncludeWarning)
            _severityFilter.Add(SchemaIssueSeverity.Warning);
        if (IncludeCritical)
            _severityFilter.Add(SchemaIssueSeverity.Critical);

        if (applyFilters)
            ApplyFilters();
    }

    private void RebuildRuleFilterFromFlags(bool applyFilters = true)
    {
        _ruleFilter.Clear();
        if (IncludeFkCatalogInconsistent)
            _ruleFilter.Add(SchemaRuleCode.FK_CATALOG_INCONSISTENT);
        if (IncludeMissingFk)
            _ruleFilter.Add(SchemaRuleCode.MISSING_FK);
        if (IncludeNamingConventionViolation)
            _ruleFilter.Add(SchemaRuleCode.NAMING_CONVENTION_VIOLATION);
        if (IncludeLowSemanticName)
            _ruleFilter.Add(SchemaRuleCode.LOW_SEMANTIC_NAME);
        if (IncludeMissingRequiredComment)
            _ruleFilter.Add(SchemaRuleCode.MISSING_REQUIRED_COMMENT);
        if (IncludeNf1HintMultiValued)
            _ruleFilter.Add(SchemaRuleCode.NF1_HINT_MULTI_VALUED);
        if (IncludeNf2HintPartialDependency)
            _ruleFilter.Add(SchemaRuleCode.NF2_HINT_PARTIAL_DEPENDENCY);
        if (IncludeNf3HintTransitiveDependency)
            _ruleFilter.Add(SchemaRuleCode.NF3_HINT_TRANSITIVE_DEPENDENCY);

        if (applyFilters)
            ApplyFilters();
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
