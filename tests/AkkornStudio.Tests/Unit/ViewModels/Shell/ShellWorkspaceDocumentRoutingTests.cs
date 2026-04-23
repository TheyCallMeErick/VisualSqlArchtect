using AkkornStudio.UI.Services.Workspace.Models;
using AkkornStudio.UI.Serialization;
using AkkornStudio.UI.ViewModels;
using AkkornStudio.Core;

namespace AkkornStudio.Tests.Unit.ViewModels.Shell;

public class ShellWorkspaceDocumentRoutingTests
{
    [Fact]
    public void EnterCanvas_RegistersAndActivatesQueryDocument()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        shell.EnterCanvas();

        Assert.Single(shell.OpenWorkspaceDocuments);
        OpenWorkspaceDocument active = Assert.IsType<OpenWorkspaceDocument>(shell.ActiveWorkspaceDocument);
        Assert.Equal(WorkspaceDocumentType.QueryCanvas, active.Descriptor.DocumentType);
        Assert.Equal(active.Descriptor.DocumentId, shell.ActiveWorkspaceDocumentId);
    }

    [Fact]
    public void EnterCanvas_DoesNotCreateDdlOrSqlEditorDocumentsUntilRequested()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        shell.EnterCanvas();

        Assert.Single(shell.OpenWorkspaceDocuments);
        Assert.Equal(WorkspaceDocumentType.QueryCanvas, shell.OpenWorkspaceDocuments[0].Descriptor.DocumentType);
    }

    [Fact]
    public void SetActiveDocumentType_SwitchesActiveWorkspaceDocumentByType()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();

        shell.ActivateDocument(WorkspaceDocumentType.DdlCanvas);
        OpenWorkspaceDocument ddlActive = Assert.IsType<OpenWorkspaceDocument>(shell.ActiveWorkspaceDocument);
        Assert.Equal(WorkspaceDocumentType.DdlCanvas, ddlActive.Descriptor.DocumentType);

        shell.ActivateDocument(WorkspaceDocumentType.SqlEditor);
        OpenWorkspaceDocument sqlEditorActive = Assert.IsType<OpenWorkspaceDocument>(shell.ActiveWorkspaceDocument);
        Assert.Equal(WorkspaceDocumentType.SqlEditor, sqlEditorActive.Descriptor.DocumentType);

        shell.ActivateDocument(WorkspaceDocumentType.QueryCanvas);
        OpenWorkspaceDocument queryActive = Assert.IsType<OpenWorkspaceDocument>(shell.ActiveWorkspaceDocument);
        Assert.Equal(WorkspaceDocumentType.QueryCanvas, queryActive.Descriptor.DocumentType);
    }

    [Fact]
    public void SetActiveDocumentType_SqlEditor_CreatesSingleSqlEditorDocumentLazily()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();

        shell.ActivateDocument(WorkspaceDocumentType.SqlEditor);
        shell.ActivateDocument(WorkspaceDocumentType.SqlEditor);

        int sqlEditorDocumentCount = 0;
        foreach (OpenWorkspaceDocument document in shell.OpenWorkspaceDocuments)
        {
            if (document.Descriptor.DocumentType == WorkspaceDocumentType.SqlEditor)
                sqlEditorDocumentCount++;
        }

        Assert.Equal(1, sqlEditorDocumentCount);
        Assert.Equal(2, shell.OpenWorkspaceDocuments.Count);
    }

    [Fact]
    public void SetActiveDocumentType_ErDiagram_CreatesSingleErDiagramDocumentLazily()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();

        shell.ActivateDocument(WorkspaceDocumentType.ErDiagram);
        shell.ActivateDocument(WorkspaceDocumentType.ErDiagram);

        int erDocumentCount = shell.OpenWorkspaceDocuments.Count(document =>
            document.Descriptor.DocumentType == WorkspaceDocumentType.ErDiagram);

        Assert.Equal(1, erDocumentCount);
        Assert.Equal(2, shell.OpenWorkspaceDocuments.Count);
        Assert.True(shell.IsErDiagramDocumentPageActive);
        Assert.NotNull(shell.ActiveErDiagramDocument);
    }

    [Fact]
    public void OpenNewDocument_WhenTypeAlreadyExists_ActivatesExistingInsteadOfCreatingDuplicate()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();
        Guid firstSqlEditorId = shell.OpenNewDocument(WorkspaceDocumentType.SqlEditor);

        Guid secondSqlEditorId = shell.OpenNewDocument(WorkspaceDocumentType.SqlEditor);

        Assert.Equal(firstSqlEditorId, secondSqlEditorId);
        int sqlEditorDocumentCount = 0;
        foreach (OpenWorkspaceDocument document in shell.OpenWorkspaceDocuments)
        {
            if (document.Descriptor.DocumentType == WorkspaceDocumentType.SqlEditor)
                sqlEditorDocumentCount++;
        }

        Assert.Equal(1, sqlEditorDocumentCount);
    }

    [Fact]
    public void SetActiveDocumentType_SqlEditor_HidesDiagramOnlyOverlaysDefensively()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();
        CanvasViewModel queryCanvas = Assert.IsType<CanvasViewModel>(shell.ActiveQueryCanvasDocument);
        queryCanvas.ConnectionManager.IsVisible = true;
        shell.OutputPreview.IsVisible = true;

        Assert.True(shell.IsConnectionManagerOverlayVisible);
        Assert.True(shell.IsOutputPreviewModalVisible);

        shell.ActivateDocument(WorkspaceDocumentType.SqlEditor);

        Assert.False(shell.IsDiagramOverlayLayerVisible);
        Assert.False(shell.IsConnectionManagerOverlayVisible);
        Assert.False(shell.IsOutputPreviewModalVisible);
        Assert.False(shell.OutputPreview.IsVisible);
        Assert.False(queryCanvas.ConnectionManager.IsVisible);
    }

    [Fact]
    public void IsConnectionManagerOverlayVisible_TracksDdlConnectionManagerWhenDdlDocumentIsActive()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();
        shell.ActivateDocument(WorkspaceDocumentType.DdlCanvas);

        CanvasViewModel ddlCanvas = Assert.IsType<CanvasViewModel>(shell.ActiveDdlCanvasDocument);
        ddlCanvas.ConnectionManager.Open();

        Assert.Same(ddlCanvas.ConnectionManager, shell.ActiveConnectionManager);
        Assert.True(shell.IsConnectionManagerVisible);
        Assert.True(shell.IsConnectionManagerOverlayVisible);
    }

    [Fact]
    public void IsConnectionManagerOverlayVisible_UsesVisibleSharedManagerWhenDdlDocumentIsActive()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();
        shell.ActivateDocument(WorkspaceDocumentType.DdlCanvas);

        CanvasViewModel queryCanvas = shell.EnsureCanvas();
        queryCanvas.ConnectionManager.Open();

        Assert.Same(queryCanvas.ConnectionManager, shell.ActiveConnectionManager);
        Assert.True(shell.IsConnectionManagerVisible);
        Assert.True(shell.IsConnectionManagerOverlayVisible);
    }

    [Fact]
    public void IsConnectionManagerOverlayVisible_AllowsConnectionModalInSqlEditorMode()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();
        shell.ActivateDocument(WorkspaceDocumentType.SqlEditor);

        CanvasViewModel queryCanvas = shell.EnsureCanvas();
        queryCanvas.ConnectionManager.Open();

        Assert.Same(queryCanvas.ConnectionManager, shell.ActiveConnectionManager);
        Assert.True(shell.IsConnectionManagerVisible);
        Assert.True(shell.IsConnectionManagerOverlayVisible);
    }

    [Fact]
    public void SqlEditorMode_ExplainPreview_DoesNotRequireSwitchingToCanvas()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();
        shell.ActivateDocument(WorkspaceDocumentType.SqlEditor);

        bool opened = shell.TryOpenSqlExplainPreview(
            sql: "SELECT 1",
            provider: DatabaseProvider.Postgres,
            connectionConfig: null);

        Assert.True(opened);
        Assert.Equal(WorkspaceDocumentType.SqlEditor, shell.ActiveWorkspaceDocumentType);
        Assert.True(shell.OutputPreview.IsVisible);
        Assert.True(shell.OutputPreview.IsSqlExplainMode);
    }

    [Fact]
    public void SqlEditorMode_BenchmarkPreview_DoesNotRequireSwitchingToCanvas()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();
        shell.ActivateDocument(WorkspaceDocumentType.SqlEditor);

        bool opened = shell.TryOpenSqlBenchmarkPreview(
            sql: "SELECT 1",
            connectionConfig: null);

        Assert.True(opened);
        Assert.Equal(WorkspaceDocumentType.SqlEditor, shell.ActiveWorkspaceDocumentType);
        Assert.True(shell.OutputPreview.IsVisible);
        Assert.True(shell.OutputPreview.IsSqlBenchmarkMode);
    }

    [Fact]
    public void TryActivateWorkspaceDocument_WithUnknownId_KeepsCurrentActiveDocument()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();
        OpenWorkspaceDocument activeBefore = Assert.IsType<OpenWorkspaceDocument>(shell.ActiveWorkspaceDocument);

        bool changed = shell.TryActivateWorkspaceDocument(Guid.NewGuid());

        Assert.False(changed);
        OpenWorkspaceDocument activeAfter = Assert.IsType<OpenWorkspaceDocument>(shell.ActiveWorkspaceDocument);
        Assert.Equal(activeBefore.Descriptor.DocumentId, activeAfter.Descriptor.DocumentId);
    }

    [Fact]
    public void CloseActiveWorkspaceDocument_ActivatesNextDocumentDeterministically()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();
        Guid ddlId = shell.OpenNewDocument(WorkspaceDocumentType.DdlCanvas);
        Guid sqlId = shell.OpenNewDocument(WorkspaceDocumentType.SqlEditor);
        Assert.True(shell.TryActivateWorkspaceDocument(ddlId));

        bool closed = shell.TryCloseWorkspaceDocument(ddlId);

        Assert.True(closed);
        OpenWorkspaceDocument active = Assert.IsType<OpenWorkspaceDocument>(shell.ActiveWorkspaceDocument);
        Assert.Equal(sqlId, active.Descriptor.DocumentId);
    }

    [Fact]
    public void RestoreWorkspaceDocuments_RebuildsDocumentOrderAndActiveSelection()
    {
        var shell = new ShellViewModel(connectionManagerViewModelFactory: global::AkkornStudio.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());
        shell.EnterCanvas();

        Guid queryId = Guid.NewGuid();
        Guid ddlId = Guid.NewGuid();
        Guid sqlId = Guid.NewGuid();
        var workspace = new SavedWorkspaceDocumentsCanvas(
            Version: 5,
            Documents:
            [
                new SavedWorkspaceDocument(
                    DocumentId: queryId,
                    DocumentType: WorkspaceDocumentType.QueryCanvas.ToString(),
                    Title: "Query A",
                    IsDirty: false,
                    PersistenceSchemaVersion: "1.0",
                    CanvasPayload: EmptyCanvasPayload()),
                new SavedWorkspaceDocument(
                    DocumentId: ddlId,
                    DocumentType: WorkspaceDocumentType.DdlCanvas.ToString(),
                    Title: "DDL A",
                    IsDirty: false,
                    PersistenceSchemaVersion: "1.0",
                    CanvasPayload: EmptyCanvasPayload()),
                new SavedWorkspaceDocument(
                    DocumentId: sqlId,
                    DocumentType: WorkspaceDocumentType.SqlEditor.ToString(),
                    Title: "SQL A",
                    IsDirty: false,
                    PersistenceSchemaVersion: "1.0")
            ],
            ActiveDocumentId: ddlId);

        shell.RestoreWorkspaceDocuments(workspace);

        Assert.Equal(3, shell.OpenWorkspaceDocuments.Count);
        Assert.Equal(queryId, shell.OpenWorkspaceDocuments[0].Descriptor.DocumentId);
        Assert.Equal(ddlId, shell.OpenWorkspaceDocuments[1].Descriptor.DocumentId);
        Assert.Equal(sqlId, shell.OpenWorkspaceDocuments[2].Descriptor.DocumentId);
        Assert.Equal(ddlId, shell.ActiveWorkspaceDocumentId);
        Assert.Equal(WorkspaceDocumentType.DdlCanvas, shell.ActiveWorkspaceDocumentType);
    }

    private static SavedCanvas EmptyCanvasPayload()
    {
        return new SavedCanvas(
            Version: 3,
            DatabaseProvider: null,
            ConnectionName: null,
            Zoom: 1,
            PanX: 0,
            PanY: 0,
            Nodes: [],
            Connections: [],
            SelectBindings: [],
            WhereBindings: [],
            AppVersion: "test",
            CreatedAt: DateTime.UtcNow.ToString("o"));
    }
}
