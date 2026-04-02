using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

public sealed class ParameterRowViewModel(NodeParameter param, string? currentValue) : ViewModelBase
{
    private string? _value = currentValue ?? param.DefaultValue;
    private bool _isDirty;

    public string Name { get; } = param.Name;
    public ParameterKind Kind { get; } = param.Kind;
    public string? Description { get; } = param.Description;
    public IReadOnlyList<string>? EnumValues { get; } = param.EnumValues;

    public bool IsText => Kind is ParameterKind.Text or ParameterKind.JsonPath;
    public bool IsNumber => Kind == ParameterKind.Number;
    public bool IsBoolean => Kind == ParameterKind.Boolean;
    public bool IsEnum => Kind is ParameterKind.Enum or ParameterKind.CastType;
    public bool IsDateTime => Kind == ParameterKind.DateTime;
    public bool IsDate => Kind == ParameterKind.Date;

    public string? Value
    {
        get => _value;
        set
        {
            if (Set(ref _value, value))
                IsDirty = true;
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => Set(ref _isDirty, value);
    }

    public void MarkClean() => IsDirty = false;
}
