using System.Windows.Input;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.Nodes.PinTypes;

namespace VisualSqlArchitect.UI.ViewModels;

public sealed class NodeTypeItemViewModel : ViewModelBase
{
    private bool _isHovered;

    public NodeDefinition Definition { get; }
    public string Title => Definition.DisplayName;
    public string Subtitle => Definition.Description;
    public string Color { get; }
    public IReadOnlyList<NodePinTooltipItemViewModel> InputPins { get; }
    public IReadOnlyList<NodePinTooltipItemViewModel> OutputPins { get; }
    public bool HasInputPins => InputPins.Count > 0;
    public bool HasOutputPins => OutputPins.Count > 0;

    public bool IsHovered
    {
        get => _isHovered;
        set => Set(ref _isHovered, value);
    }

    public ICommand SpawnNodeCommand { get; }

    public NodeTypeItemViewModel(NodeDefinition definition, string color, Action<NodeDefinition> onSpawn)
    {
        Definition = definition;
        Color = color;
        InputPins = [.. definition.InputPins.Select(BuildPinTooltipItem)];
        OutputPins = [.. definition.OutputPins.Select(BuildPinTooltipItem)];
        SpawnNodeCommand = new RelayCommand(() => onSpawn(definition));
    }

    private static NodePinTooltipItemViewModel BuildPinTooltipItem(PinDescriptor pin)
    {
        string color = PinTypeRegistry.GetType(pin.DataType).VisualColorHex;
        (string glyph, string name) = ResolvePinShape(pin.DataType);
        return new NodePinTooltipItemViewModel(
            Name: pin.Name,
            TypeName: pin.DataType.ToString(),
            Color: color,
            ShapeGlyph: glyph,
            ShapeName: name);
    }

    private static (string Glyph, string Name) ResolvePinShape(PinDataType dataType) =>
        dataType switch
        {
            PinDataType.ColumnRef => ("◆", "Diamond"),
            PinDataType.ColumnSet => ("◇", "Diamond"),
            PinDataType.RowSet => ("◆", "Flat Diamond"),
            PinDataType.TableDef => ("■", "Rounded Square"),
            PinDataType.ViewDef => ("■", "Rounded Square"),
            PinDataType.ColumnDef => ("◎", "Double Circle"),
            PinDataType.Constraint => ("◆", "Diamond"),
            PinDataType.TypeDef => ("◇", "Double Diamond"),
            PinDataType.IndexDef => ("▲", "Triangle"),
            PinDataType.AlterOp => ("➤", "Rounded Arrow"),
            _ => ("●", "Circle"),
        };
}
