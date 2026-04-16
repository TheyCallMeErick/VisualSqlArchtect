using AkkornStudio.Nodes;

namespace AkkornStudio.Nodes.LogicalPlan;

public sealed record LogicalJoin(
    string NodeId,
    LogicalNode Left,
    LogicalNode Right,
    JoinKind Kind,
    ISqlExpression Condition) : LogicalNode;
