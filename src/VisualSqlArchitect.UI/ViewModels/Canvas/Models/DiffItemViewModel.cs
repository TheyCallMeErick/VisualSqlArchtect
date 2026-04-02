using Avalonia.Media;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

public sealed class DiffItemViewModel
{
    public EDiffKind Kind { get; }
    public string Description { get; }

    public string Icon =>
        Kind switch
        {
            EDiffKind.Added => "+",
            EDiffKind.Removed => "−",
            _ => "~",
        };

    public Color KindColor =>
        Kind switch
        {
            EDiffKind.Added => Color.Parse("#4ADE80"),
            EDiffKind.Removed => Color.Parse("#F87171"),
            _ => Color.Parse("#FBBF24"),
        };

    public SolidColorBrush KindBrush => new(KindColor);

    public DiffItemViewModel(EDiffKind kind, string description)
    {
        Kind = kind;
        Description = description;
    }
}
