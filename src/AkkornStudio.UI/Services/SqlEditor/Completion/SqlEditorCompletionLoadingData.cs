using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlEditorCompletionLoadingData : ICompletionData
{
    private static readonly IBrush LoadingForegroundBrush = new SolidColorBrush(Color.Parse("#B8C3D9"));
    private readonly TextBlock _content;

    public SqlEditorCompletionLoadingData(string message)
    {
        string normalized = string.IsNullOrWhiteSpace(message) ? "Carregando sugestoes..." : message;
        Text = normalized;
        Description = normalized;
        _content = new TextBlock
        {
            Text = normalized,
            Foreground = LoadingForegroundBrush,
            FontStyle = FontStyle.Italic,
        };
    }

    public IImage? Image => null;

    public string Text { get; }

    public object Content => _content;

    public object Description { get; }

    public double Priority => double.MinValue;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // Loading entry is informational only and should never mutate editor text.
    }
}
