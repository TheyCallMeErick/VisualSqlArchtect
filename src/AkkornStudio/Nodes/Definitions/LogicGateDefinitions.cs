using AkkornStudio.Expressions;
using AkkornStudio.Nodes;
using static AkkornStudio.Nodes.Definitions.NodeDefinitionHelpers;

namespace AkkornStudio.Nodes.Definitions;

public static class LogicGateDefinitions
{
    private static readonly IReadOnlyList<AkkornStudio.Nodes.NodeParameter> EmptyParams = [];

    public static readonly NodeDefinition And = new(
        NodeType.And,
        NodeCategory.LogicGate,
        "AND",
        "All conditions must be true",
        [
            In("conditions", PinDataType.Boolean, required: false, multi: true),
            Out("result", PinDataType.Boolean),
        ],
        EmptyParams
    );

    public static readonly NodeDefinition Or = new(
        NodeType.Or,
        NodeCategory.LogicGate,
        "OR",
        "At least one condition must be true",
        [
            In("conditions", PinDataType.Boolean, required: false, multi: true),
            Out("result", PinDataType.Boolean),
        ],
        EmptyParams
    );

    public static readonly NodeDefinition Not = new(
        NodeType.Not,
        NodeCategory.LogicGate,
        "NOT",
        "Negates a boolean expression",
        [In("condition", PinDataType.Boolean), Out("result", PinDataType.Boolean)],
        EmptyParams
    );
}
