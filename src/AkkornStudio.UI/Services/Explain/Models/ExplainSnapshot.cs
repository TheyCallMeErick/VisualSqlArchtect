using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.Explain;

public sealed record ExplainSnapshot(
    string Label,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ExplainStep> Steps
);
