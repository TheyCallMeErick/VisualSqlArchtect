namespace DBWeaver.UI.Services.SqlEditor;

public sealed record SqlCompletionTelemetry
{
    public long WorkerDispatchDelayMs { get; init; }
    public long WorkerExecutionMs { get; init; }
    public long TokenizationMs { get; init; }
    public long StatementExtractionMs { get; init; }
    public long ContextDetectionMs { get; init; }
    public long SymbolTableMs { get; init; }
    public long MetadataLookupMs { get; init; }
    public long FuzzyMs { get; init; }
    public long RequestBuildMs { get; init; }
    public long LightweightBuildMs { get; init; }
    public long RankedBuildMs { get; init; }
    public long RankingMs { get; init; }
    public long TotalMs { get; init; }
    public long TimeToFirstSuggestionMs { get; init; }
    public int CancelledRequests { get; init; }
    public long BudgetMs { get; init; } = 100;

    public bool IsWithinBudget => TotalMs <= BudgetMs;
}
