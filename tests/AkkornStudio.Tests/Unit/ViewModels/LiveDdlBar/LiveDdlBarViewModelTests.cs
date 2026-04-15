using Avalonia;
using AkkornStudio.Core;
using AkkornStudio.Metadata;
using AkkornStudio.Nodes;
using AkkornStudio.UI.Services.Localization;
using AkkornStudio.UI.ViewModels;
using AkkornStudio.UI.ViewModels.Canvas.Strategies;

namespace AkkornStudio.Tests.Unit.ViewModels.LiveDdlBar;

public class LiveDdlBarViewModelTests
{
    [Fact]
    public void Recompile_ProducesCreateTableSql_ForConnectedDdlGraph()
    {
        var ddlCanvas = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy());

        var table = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        table.Parameters["SchemaName"] = "dbo";
        table.Parameters["TableName"] = "orders";
        table.Parameters["IfNotExists"] = "true";

        var col = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));
        col.Parameters["ColumnName"] = "id";
        col.Parameters["DataType"] = "INT";
        col.Parameters["IsNullable"] = "false";

        var output = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.CreateTableOutput), new Point(0, 0));

        ddlCanvas.Nodes.Add(table);
        ddlCanvas.Nodes.Add(col);
        ddlCanvas.Nodes.Add(output);

        ddlCanvas.Connections.Add(new ConnectionViewModel(
            col.OutputPins.First(p => p.Name == "column"),
            new Point(0, 0),
            new Point(10, 10)
        ) { ToPin = table.InputPins.First(p => p.Name == "column") });

        ddlCanvas.Connections.Add(new ConnectionViewModel(
            table.OutputPins.First(p => p.Name == "table"),
            new Point(0, 0),
            new Point(10, 10)
        ) { ToPin = output.InputPins.First(p => p.Name == "table") });

        Assert.NotNull(ddlCanvas.LiveDdl);
        ddlCanvas.LiveDdl!.Recompile();

        Assert.True(ddlCanvas.LiveDdl.IsValid);
        Assert.Contains("CREATE TABLE [dbo].[orders]", ddlCanvas.LiveDdl.RawSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Recompile_GeneratesSql_WhenOnlyDefinitionsExist_AndNoOutputNode()
    {
        var ddlCanvas = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy());

        var table = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.TableDefinition), new Point(0, 0));
        table.Parameters["SchemaName"] = "dbo";
        table.Parameters["TableName"] = "orders";

        var col = new NodeViewModel(NodeDefinitionRegistry.Get(NodeType.ColumnDefinition), new Point(0, 0));
        col.Parameters["ColumnName"] = "id";
        col.Parameters["DataType"] = "INT";
        col.Parameters["IsNullable"] = "false";

        ddlCanvas.Nodes.Add(table);
        ddlCanvas.Nodes.Add(col);

        ddlCanvas.Connections.Add(new ConnectionViewModel(
            col.OutputPins.First(p => p.Name == "column"),
            new Point(0, 0),
            new Point(10, 10)
        ) { ToPin = table.InputPins.First(p => p.Name == "column") });

        Assert.NotNull(ddlCanvas.LiveDdl);
        ddlCanvas.LiveDdl!.Recompile();

        Assert.True(ddlCanvas.LiveDdl.IsValid);
        Assert.Contains("CREATE TABLE [dbo].[orders]", ddlCanvas.LiveDdl.RawSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeSchemaStructureAsync_WhenMetadataMissing_SetsIdleState()
    {
        var ddlCanvas = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy());
        LiveDdlBarViewModel liveDdl = Assert.IsType<LiveDdlBarViewModel>(ddlCanvas.LiveDdl);

        await liveDdl.AnalyzeSchemaStructureAsync();

        Assert.Equal(SchemaAnalysisViewState.Idle, liveDdl.SchemaAnalysisPanel.State);
        Assert.Equal(
            L("preview.schemaAnalysis.state.metadataUnavailable", "Metadata indisponível para análise estrutural."),
            liveDdl.SchemaAnalysisPanel.StateMessage
        );
    }

    [Fact]
    public async Task AnalyzeSchemaStructureAsync_WithMetadata_ProducesTerminalState()
    {
        var ddlCanvas = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy())
        {
            DatabaseMetadata = CreateMetadata(),
        };

        LiveDdlBarViewModel liveDdl = Assert.IsType<LiveDdlBarViewModel>(ddlCanvas.LiveDdl);

        await liveDdl.AnalyzeSchemaStructureAsync();

        Assert.False(liveDdl.IsRunningSchemaAnalysis);
        Assert.True(
            liveDdl.SchemaAnalysisPanel.State == SchemaAnalysisViewState.Completed
            || liveDdl.SchemaAnalysisPanel.State == SchemaAnalysisViewState.Empty
            || liveDdl.SchemaAnalysisPanel.State == SchemaAnalysisViewState.Partial
            || liveDdl.SchemaAnalysisPanel.State == SchemaAnalysisViewState.Failed
        );
    }

    [Fact]
    public async Task AnalyzeSchemaStructureAsync_WithCanceledToken_SetsCancelledState()
    {
        var ddlCanvas = new CanvasViewModel(null, null, null, null, new DdlDomainStrategy())
        {
            DatabaseMetadata = CreateMetadata(),
        };

        LiveDdlBarViewModel liveDdl = Assert.IsType<LiveDdlBarViewModel>(ddlCanvas.LiveDdl);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await liveDdl.AnalyzeSchemaStructureAsync(cts.Token);

        Assert.Equal(SchemaAnalysisViewState.Cancelled, liveDdl.SchemaAnalysisPanel.State);
    }

    private static DbMetadata CreateMetadata()
    {
        var idColumn = new ColumnMetadata(
            Name: "id",
            DataType: "int",
            NativeType: "int",
            IsNullable: false,
            IsPrimaryKey: true,
            IsForeignKey: false,
            IsUnique: true,
            IsIndexed: true,
            OrdinalPosition: 1,
            Comment: ""
        );

        var table = new TableMetadata(
            Schema: "public",
            Name: "customers",
            Kind: TableKind.Table,
            EstimatedRowCount: 100,
            Columns: [idColumn],
            Indexes:
            [
                new IndexMetadata(
                    Name: "pk_customers",
                    IsUnique: true,
                    IsClustered: false,
                    IsPrimaryKey: true,
                    Columns: ["id"]
                ),
            ],
            OutboundForeignKeys: [],
            InboundForeignKeys: [],
            Comment: ""
        );

        return new DbMetadata(
            DatabaseName: "db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", [table])],
            AllForeignKeys: []
        );
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
