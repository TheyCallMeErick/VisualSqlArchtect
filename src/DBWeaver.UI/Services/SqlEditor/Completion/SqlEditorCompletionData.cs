using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Material.Icons;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorCompletionData : ICompletionData
{
    private readonly string _insertText;
    private readonly SqlCompletionKind _kind;
    private readonly int _prefixLength;
    private readonly SqlEditorCompletionItemContent _content;
    private readonly Action<string>? _acceptedCallback;

    public SqlEditorCompletionData(
        string label,
        string insertText,
        string? description,
        SqlCompletionKind kind,
        int prefixLength,
        Action<string>? acceptedCallback = null)
    {
        Text = label;
        _insertText = insertText;
        string normalizedDescription = description ?? string.Empty;
        Description = normalizedDescription;
        _kind = kind;
        _prefixLength = Math.Max(0, prefixLength);
        _content = new SqlEditorCompletionItemContent(
            label,
            normalizedDescription,
            kind,
            ResolveIconKind(kind),
            ResolveIconForeground(kind),
            ResolveLabelForeground(kind));
        _acceptedCallback = acceptedCallback;
    }

    public IImage? Image => null;

    public string Text { get; }

    public object Content => _content;

    public object Description { get; }

    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        int replaceStart = Math.Max(0, textArea.Caret.Offset - _prefixLength);
        if (_kind == SqlCompletionKind.Snippet)
        {
            SqlEditorSnippetTemplate template = SqlEditorSnippetTemplateParser.Parse(_insertText);
            textArea.Document.Replace(replaceStart, _prefixLength, template.Text);
            SqlEditorSnippetTabStopSessionStore.Clear(textArea.Document);
            if (template.TabStopOffsets.Count > 0)
            {
                SqlEditorSnippetTabStopSession session = SqlEditorSnippetTabStopSession.Create(
                    textArea.Document,
                    replaceStart,
                    template.TabStopOffsets);
                SqlEditorSnippetTabStopSessionStore.Set(textArea.Document, session);
                _ = session.MoveToNext(textArea);
            }
        }
        else
        {
            textArea.Document.Replace(replaceStart, _prefixLength, _insertText);
            SqlEditorSnippetTabStopSessionStore.Clear(textArea.Document);
        }

        _acceptedCallback?.Invoke(Text);
    }

    private static MaterialIconKind ResolveIconKind(SqlCompletionKind kind)
    {
        return kind switch
        {
            SqlCompletionKind.Keyword => MaterialIconKind.CodeBraces,
            SqlCompletionKind.Table => MaterialIconKind.Table,
            SqlCompletionKind.Column => MaterialIconKind.TableColumn,
            SqlCompletionKind.Function => MaterialIconKind.Function,
            SqlCompletionKind.Snippet => MaterialIconKind.ContentPaste,
            SqlCompletionKind.Join => MaterialIconKind.SetMerge,
            _ => MaterialIconKind.Text,
        };
    }

    private static IBrush ResolveIconForeground(SqlCompletionKind kind)
    {
        return kind switch
        {
            SqlCompletionKind.Keyword => new SolidColorBrush(Color.Parse("#8FB7FF")),
            SqlCompletionKind.Table => new SolidColorBrush(Color.Parse("#45D69F")),
            SqlCompletionKind.Column => new SolidColorBrush(Color.Parse("#AFC5FF")),
            SqlCompletionKind.Function => new SolidColorBrush(Color.Parse("#FFBF63")),
            SqlCompletionKind.Snippet => new SolidColorBrush(Color.Parse("#D8A6FF")),
            SqlCompletionKind.Join => new SolidColorBrush(Color.Parse("#4CC8D9")),
            _ => new SolidColorBrush(Color.Parse("#B9C2D1")),
        };
    }

    private static IBrush ResolveLabelForeground(SqlCompletionKind kind)
    {
        return kind switch
        {
            SqlCompletionKind.Keyword => new SolidColorBrush(Color.Parse("#D7E4FF")),
            SqlCompletionKind.Table => new SolidColorBrush(Color.Parse("#C3F8E5")),
            SqlCompletionKind.Column => new SolidColorBrush(Color.Parse("#D8E3FF")),
            SqlCompletionKind.Function => new SolidColorBrush(Color.Parse("#FFE2BA")),
            SqlCompletionKind.Snippet => new SolidColorBrush(Color.Parse("#F1DBFF")),
            SqlCompletionKind.Join => new SolidColorBrush(Color.Parse("#CCF4F8")),
            _ => new SolidColorBrush(Color.Parse("#E2E7EF")),
        };
    }
}
