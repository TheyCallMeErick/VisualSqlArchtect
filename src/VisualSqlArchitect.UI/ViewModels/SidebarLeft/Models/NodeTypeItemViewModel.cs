using System.Windows.Input;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

public sealed class NodeTypeItemViewModel : ViewModelBase
{
    private bool _isHovered;

    public NodeDefinition Definition { get; }
    public string Title => Definition.DisplayName;
    public string Subtitle => Definition.Description;
    public string Color { get; }

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
        SpawnNodeCommand = new RelayCommand(() => onSpawn(definition));
    }
}
