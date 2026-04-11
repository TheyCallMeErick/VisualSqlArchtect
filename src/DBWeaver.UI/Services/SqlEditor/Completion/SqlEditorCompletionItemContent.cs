using Material.Icons;
using Avalonia.Media;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed record SqlEditorCompletionItemContent(
    string Label,
    string Description,
    SqlCompletionKind Kind,
    MaterialIconKind IconKind,
    IBrush IconForeground,
    IBrush LabelForeground);
