using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.Explain;

public sealed record SqlServerParsedPlan(
    IReadOnlyList<ExplainNode> Nodes,
    double? PlanningTimeMs,
    double? ExecutionTimeMs
);



