namespace AkkornStudio.Nodes.LogicalPlan;

public sealed record LogicalProjection(
    string NodeId,
    LogicalNode Source,
    IReadOnlyList<ProjectionBinding> Columns) : LogicalNode;
