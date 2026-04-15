namespace AkkornStudio.Nodes.Definitions;

using AkkornStudio.Nodes;
using static NodeDefinitionHelpers;

/// <summary>
/// Data source node definitions.
/// Defines nodes for selecting and aliasing data sources.
/// </summary>
public static class DataSourceDefinitions
{
    public static readonly AkkornStudio.Nodes.NodeDefinition Alias = new(
        NodeType.Alias,
        NodeCategory.DataSource,
        "ALIAS (AS)",
        "Renames a column or expression with AS",
        [In("expression", PinDataType.Expression), Out("result", PinDataType.ColumnRef)],
        [
            new(
                "alias",
                AkkornStudio.Nodes.ParameterKind.Text,
                null,
                "New alias name (e.g. total_price)"
            ),
        ]
    );
}
