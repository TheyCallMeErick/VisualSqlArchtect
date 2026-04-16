namespace AkkornStudio.Nodes.Pins;

public interface ISchemaCapability
{
    ColumnRefMeta? ColumnRef { get; }
    ColumnSetMeta? ColumnSet { get; }
}
