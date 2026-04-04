using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.Explain;

public sealed record ExplainSnapshot(
    string Label,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ExplainStep> Steps
);

public sealed class ExplainComparisonRow
{
    public string Operation { get; init; } = string.Empty;
    public double CostA { get; init; }
    public double CostB { get; init; }
    public double? DeltaPercent =>
        CostA > 0 ? ((CostB - CostA) / CostA) * 100.0 : null;
    public string CostAText => CostA.ToString("0.##");
    public string CostBText => CostB.ToString("0.##");
    public string DeltaText => DeltaPercent.HasValue ? $"{DeltaPercent.Value:+0.#;-0.#;0}%" : "n/a";
    public bool IsImproved => DeltaPercent is < -0.01;
    public bool IsRegressed => DeltaPercent is > 0.01;
    public string DeltaColor => IsImproved ? "#86EFAC" : IsRegressed ? "#FCA5A5" : "#9CA3AF";
    public string StatusLabel => IsImproved ? "IMPROVED" : IsRegressed ? "REGRESSED" : "UNCHANGED";
}



