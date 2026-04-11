namespace DBWeaver.UI.Services.SqlEditor;

public sealed record SqlEditorSnippetTemplate(
    string Text,
    IReadOnlyList<int> TabStopOffsets);
