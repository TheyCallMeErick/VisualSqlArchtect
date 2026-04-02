namespace VisualSqlArchitect.UI.ViewModels.Canvas;

public sealed class ExplainStep
{
    public int StepNumber { get; init; }
    public string Operation { get; init; } = "";
    public string? Detail { get; init; }
    public double? EstimatedCost { get; init; }
    public long? EstimatedRows { get; init; }
    public int IndentLevel { get; init; }
    public bool IsExpensive { get; init; }
    public string AlertLabel { get; init; } = "";

    public double IndentMargin => IndentLevel * 18.0;
    public string CostText => EstimatedCost.HasValue ? $"{EstimatedCost:F2}" : "–";
    public string RowsText => EstimatedRows.HasValue ? $"{EstimatedRows:N0}" : "–";

    public string AlertColor => AlertLabel switch
    {
        "SEQ SCAN" => "#FBBF24",
        "SORT" => "#FB923C",
        "HASH" => "#60A5FA",
        "LOOP" => "#A78BFA",
        _ => "#6B7280",
    };

    public bool HasAlert => !string.IsNullOrEmpty(AlertLabel);
}
