using AkkornStudio.Nodes;

namespace AkkornStudio.Nodes.LogicalPlan;

public sealed record ColumnOrigin(string DatasetAlias, string ColumnName, PinDataType Type);
