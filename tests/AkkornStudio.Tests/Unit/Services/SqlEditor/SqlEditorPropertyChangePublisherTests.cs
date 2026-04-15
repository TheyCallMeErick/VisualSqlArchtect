using AkkornStudio.UI.Services.SqlEditor;

namespace AkkornStudio.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorPropertyChangePublisherTests
{
    [Fact]
    public void PublishSqlPanelChanges_RaisesExpectedProperties()
    {
        var sut = new SqlEditorPropertyChangePublisher();
        var raised = new List<string>();

        sut.PublishSqlPanelChanges(raised.Add);

        Assert.Contains("ExecutionTelemetryText", raised);
        Assert.Contains("ResultSummaryText", raised);
        Assert.Contains("PendingMutationDiffText", raised);
        Assert.DoesNotContain("SchemaTables", raised);
        Assert.DoesNotContain("FilteredSchemaTables", raised);
        Assert.DoesNotContain("SchemaSearchText", raised);
        Assert.True(raised.Count >= 21);
    }

    [Fact]
    public void PublishTabStateChanges_RaisesExpectedProperties()
    {
        var sut = new SqlEditorPropertyChangePublisher();
        var raised = new List<string>();

        sut.PublishTabStateChanges(raised.Add);

        Assert.Contains("EditorTabs", raised);
        Assert.Contains("ActiveTab", raised);
        Assert.Contains("PendingCloseTabMessage", raised);
        Assert.Contains("ManyTabsWarningText", raised);
        Assert.True(raised.Count >= 7);
    }

    [Fact]
    public void PublishSchemaChanges_RaisesOnlySchemaProperties()
    {
        var sut = new SqlEditorPropertyChangePublisher();
        var raised = new List<string>();

        sut.PublishSchemaChanges(raised.Add);

        Assert.Contains("SchemaTables", raised);
        Assert.Contains("FilteredSchemaTables", raised);
        Assert.Contains("HasFilteredSchemaTables", raised);
        Assert.Contains("HasSchemaTables", raised);
        Assert.Contains("IsSchemaEmpty", raised);
        Assert.Contains("SchemaEmptyText", raised);
        Assert.DoesNotContain("ExecutionTelemetryText", raised);
        Assert.DoesNotContain("ResultSummaryText", raised);
        Assert.True(raised.Count >= 7);
    }
}

