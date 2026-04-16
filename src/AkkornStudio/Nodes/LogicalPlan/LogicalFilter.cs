using AkkornStudio.Nodes;

namespace AkkornStudio.Nodes.LogicalPlan;

public sealed record LogicalFilter(
    string NodeId,
    LogicalNode Source,
    ISqlExpression Predicate) : LogicalNode;
