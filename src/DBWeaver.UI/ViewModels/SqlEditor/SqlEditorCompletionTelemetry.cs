namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorCompletionTelemetry
{
    public int SampleCount { get; init; }
    public long LastDurationMs { get; init; }
    public long P95DurationMs { get; init; }
    public long LastEngineDurationMs { get; init; }
    public long P95EngineDurationMs { get; init; }
    public long LastDispatchDelayMs { get; init; }
    public long P95DispatchDelayMs { get; init; }
    public long LastUiApplyDurationMs { get; init; }
    public long P95UiApplyDurationMs { get; init; }
    public long BudgetMs { get; init; }

    public bool IsWithinBudget => P95DurationMs <= BudgetMs;

    public static SqlEditorCompletionTelemetry Empty(long budgetMs) => new()
    {
        SampleCount = 0,
        LastDurationMs = 0,
        P95DurationMs = 0,
        LastEngineDurationMs = 0,
        P95EngineDurationMs = 0,
        LastDispatchDelayMs = 0,
        P95DispatchDelayMs = 0,
        LastUiApplyDurationMs = 0,
        P95UiApplyDurationMs = 0,
        BudgetMs = budgetMs,
    };
}
