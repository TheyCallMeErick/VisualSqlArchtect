using AkkornStudio.Nodes;

namespace AkkornStudio.Nodes.LogicalPlan;

public sealed record AggregateBinding(string Alias, ISqlExpression Expression);
