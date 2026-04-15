namespace AkkornStudio.UI.Services.SqlEditor;

public sealed record SqlCompletionDocumentWindow(
    int StartOffset,
    int EndOffset,
    string Text)
{
    public int Length => Math.Max(0, EndOffset - StartOffset);
}
