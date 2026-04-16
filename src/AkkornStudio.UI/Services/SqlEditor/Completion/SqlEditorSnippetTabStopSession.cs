using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlEditorSnippetTabStopSession
{
    private readonly IReadOnlyList<TextAnchor> _anchors;
    private int _nextIndex;

    public SqlEditorSnippetTabStopSession(IReadOnlyList<TextAnchor> anchors)
    {
        _anchors = anchors;
    }

    public bool MoveToNext(TextArea textArea)
    {
        if (_nextIndex >= _anchors.Count)
            return false;

        TextAnchor? anchor = _anchors[_nextIndex];
        _nextIndex++;

        if (anchor.IsDeleted)
            return MoveToNext(textArea);

        int offset = Math.Clamp(anchor.Offset, 0, textArea.Document.TextLength);
        textArea.Caret.Offset = offset;
        textArea.Caret.BringCaretToView();
        return true;
    }

    public static SqlEditorSnippetTabStopSession Create(TextDocument document, int insertionStartOffset, IReadOnlyList<int> tabStopOffsets)
    {
        var anchors = new List<TextAnchor>(tabStopOffsets.Count);
        foreach (int relative in tabStopOffsets)
        {
            int absolute = Math.Clamp(insertionStartOffset + relative, 0, document.TextLength);
            TextAnchor anchor = document.CreateAnchor(absolute);
            anchor.MovementType = AnchorMovementType.BeforeInsertion;
            anchors.Add(anchor);
        }

        return new SqlEditorSnippetTabStopSession(anchors);
    }
}
