using Avalonia.Controls;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Material.Icons;
using Material.Icons.Avalonia;

namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlEditorCompletionData : ICompletionData
{
    private static readonly IBrush CompletionForegroundBrush = new SolidColorBrush(Color.Parse("#E2E7EF"));
    private static readonly IBrush CompletionDescriptionBrush = new SolidColorBrush(Color.Parse("#A6B3C8"));

    private readonly string _insertText;
    private readonly string _acceptedLabel;
    private readonly SqlCompletionKind _kind;
    private readonly int _prefixLength;
    private readonly Control _content;
    private readonly Action<string>? _acceptedCallback;

    public SqlEditorCompletionData(
        string label,
        string insertText,
        string? description,
        SqlCompletionKind kind,
        int prefixLength,
        Action<string>? acceptedCallback = null)
    {
        string normalizedLabel = ResolveDisplayLabel(label, insertText, description);
        string normalizedInsertText = string.IsNullOrWhiteSpace(insertText)
            ? normalizedLabel
            : insertText;

        _acceptedLabel = normalizedLabel;
        Text = $"{ResolveKindGlyph(kind)} {normalizedLabel}";
        _insertText = normalizedInsertText;
        string normalizedDescription = description ?? string.Empty;
        Description = normalizedDescription;
        _kind = kind;
        _prefixLength = Math.Max(0, prefixLength);
        _content = BuildContent(normalizedLabel, normalizedDescription, _kind);
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

        _acceptedCallback?.Invoke(_acceptedLabel);
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

    private static Control BuildContent(string label, string description, SqlCompletionKind kind)
    {
        MaterialIconKind iconKind = ResolveIconKind(kind);
        IBrush iconForeground = ResolveIconForeground(kind);
        IBrush labelForeground = ResolveLabelForeground(kind);

        var icon = new MaterialIcon
        {
            Kind = iconKind,
            Width = 12,
            Height = 12,
            Foreground = iconForeground,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        var iconHost = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.Parse("#1A2436")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2C3B58")),
            BorderThickness = new Thickness(1),
            Child = icon,
        };

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = labelForeground,
            FontWeight = FontWeight.SemiBold,
        };

        var descriptionBlock = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(description) ? string.Empty : description,
            Foreground = CompletionDescriptionBrush,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 360,
            IsVisible = !string.IsNullOrWhiteSpace(description),
        };

        var textStack = new StackPanel
        {
            Spacing = 1,
            Children = { labelBlock, descriptionBlock },
        };

        var row = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            MinWidth = 280,
            Children = { iconHost, textStack },
        };

        return row;
    }

    private static string ResolveDisplayLabel(string? label, string? insertText, string? description)
    {
        if (!string.IsNullOrWhiteSpace(label))
            return label;

        if (!string.IsNullOrWhiteSpace(insertText))
            return insertText;

        if (!string.IsNullOrWhiteSpace(description))
            return description;

        return "(completion)";
    }

    private static string ResolveKindGlyph(SqlCompletionKind kind)
    {
        return kind switch
        {
            SqlCompletionKind.Keyword => "[K]",
            SqlCompletionKind.Table => "[T]",
            SqlCompletionKind.Column => "[C]",
            SqlCompletionKind.Function => "[F]",
            SqlCompletionKind.Snippet => "[S]",
            SqlCompletionKind.Join => "[J]",
            _ => "[•]",
        };
    }
}
