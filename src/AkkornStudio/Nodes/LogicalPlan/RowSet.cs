namespace AkkornStudio.Nodes.LogicalPlan;

public sealed record RowSet(IReadOnlyList<ColumnDef> Schema, string Alias);
