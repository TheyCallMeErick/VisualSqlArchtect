using AkkornStudio.Nodes;

namespace AkkornStudio.Nodes.LogicalPlan;

public sealed record LogicalColumn(string Name, PinDataType Type, bool IsNullable = true);
