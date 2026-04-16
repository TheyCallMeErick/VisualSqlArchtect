using AkkornStudio.UI.Services.SqlEditor;

namespace AkkornStudio.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorSnippetTemplateParserTests
{
    [Fact]
    public void Parse_RemovesMarkersAndOrdersTabStops()
    {
        SqlEditorSnippetTemplate template = SqlEditorSnippetTemplateParser.Parse("SELECT $2 FROM $1 WHERE $0");

        Assert.Equal("SELECT  FROM  WHERE ", template.Text);
        Assert.Equal(3, template.TabStopOffsets.Count);
        Assert.Equal(template.Text.IndexOf("FROM ", StringComparison.Ordinal) + "FROM ".Length, template.TabStopOffsets[0]);
        Assert.Equal("SELECT ".Length, template.TabStopOffsets[1]);
        Assert.Equal(template.Text.Length, template.TabStopOffsets[2]);
    }

    [Fact]
    public void Parse_WithoutMarkers_ReturnsOriginalTextAndNoStops()
    {
        SqlEditorSnippetTemplate template = SqlEditorSnippetTemplateParser.Parse("SELECT * FROM public.users;");

        Assert.Equal("SELECT * FROM public.users;", template.Text);
        Assert.Empty(template.TabStopOffsets);
    }
}
