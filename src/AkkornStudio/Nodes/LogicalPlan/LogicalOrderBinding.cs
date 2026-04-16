using AkkornStudio.Nodes;

namespace AkkornStudio.Nodes.LogicalPlan;

public sealed record LogicalOrderBinding(ISqlExpression Expression, bool Descending = false);
