using AkkornStudio.Nodes;

namespace AkkornStudio.Nodes.LogicalPlan;

public sealed record ProjectionBinding(ISqlExpression Expression, string? Alias = null);
