namespace DBWeaver.UI.Services.SqlEditor;

public sealed record SqlCompletionStageSnapshot(
    SqlCompletionPipelineStage Stage,
    SqlCompletionRequest Request,
    SqlCompletionTelemetry Telemetry,
    bool IsFinal)
{
    public bool HasSuggestions => Request.Suggestions.Count > 0;
}
