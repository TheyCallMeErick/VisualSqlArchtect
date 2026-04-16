using AvaloniaEdit.Document;
using System.Runtime.CompilerServices;

namespace AkkornStudio.UI.Services.SqlEditor;

public static class SqlEditorSnippetTabStopSessionStore
{
    private static readonly ConditionalWeakTable<TextDocument, SqlEditorSnippetTabStopSession> Sessions = new();

    public static void Set(TextDocument document, SqlEditorSnippetTabStopSession session)
    {
        Sessions.Remove(document);
        Sessions.Add(document, session);
    }

    public static SqlEditorSnippetTabStopSession? TryGet(TextDocument document)
    {
        return Sessions.TryGetValue(document, out SqlEditorSnippetTabStopSession? session) ? session : null;
    }

    public static void Clear(TextDocument document)
    {
        Sessions.Remove(document);
    }
}
