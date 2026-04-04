
namespace VisualSqlArchitect.UI.Services.QueryPreview;

internal sealed class QueryCompilationInputStage(
    Func<IReadOnlyList<NodeViewModel>, Dictionary<string, string>> buildCteDefinitionNameMap,
    Func<IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, (string FromTable, string? Warning)> resolveFromTable,
    Func<PinViewModel, bool> isWildcardProjectionPin,
    Func<string?, bool> isProjectionInputPinName) : IQueryCompilationStage<QueryCompilationInputStageResult>
{
    private readonly Func<IReadOnlyList<NodeViewModel>, Dictionary<string, string>> _buildCteDefinitionNameMap = buildCteDefinitionNameMap;
    private readonly Func<IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, (string FromTable, string? Warning)> _resolveFromTable = resolveFromTable;
    private readonly Func<PinViewModel, bool> _isWildcardProjectionPin = isWildcardProjectionPin;
    private readonly Func<string?, bool> _isProjectionInputPinName = isProjectionInputPinName;

    public QueryCompilationInputStageResult Execute(QueryCompilationPipelineContext context)
    {
        var errors = new List<string>();
        CanvasViewModel canvas = context.Canvas;

        List<NodeViewModel> tableNodes = canvas.Nodes.Where(n => n.Type == NodeType.TableSource).ToList();
        List<NodeViewModel> cteSourceNodes = canvas.Nodes.Where(n => n.Type == NodeType.CteSource).ToList();
        List<NodeViewModel> subqueryNodes = canvas.Nodes.Where(n => n.Type == NodeType.Subquery).ToList();
        List<NodeViewModel> cteDefinitions = canvas.Nodes.Where(n => n.Type == NodeType.CteDefinition).ToList();
        Dictionary<string, string> cteDefinitionNamesById = _buildCteDefinitionNameMap(cteDefinitions);

        if (tableNodes.Count == 0 && cteSourceNodes.Count == 0 && subqueryNodes.Count == 0)
        {
            return new QueryCompilationInputStageResult(
                Snapshot: null,
                ShouldShortCircuit: true,
                ShortCircuitSql: "-- Add a table, CTE source, or Subquery node to start building your query",
                Errors: errors);
        }

        NodeViewModel? resultOutputNode = canvas.Nodes.FirstOrDefault(n =>
            n.Type is NodeType.ResultOutput or NodeType.SelectOutput
        );
        if (resultOutputNode is null)
        {
            return new QueryCompilationInputStageResult(
                Snapshot: null,
                ShouldShortCircuit: true,
                ShortCircuitSql: "-- Add a Result Output node to generate SQL",
                Errors: errors);
        }

        ConnectionViewModel? columnListConn = canvas.Connections.FirstOrDefault(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "columns"
            && c.FromPin.Owner.Type is NodeType.ColumnList or NodeType.ColumnSetBuilder
        );
        bool hasColumnListColumns = columnListConn is not null && canvas.Connections.Any(c =>
            c.ToPin?.Owner == columnListConn.FromPin.Owner
            && _isProjectionInputPinName(c.ToPin?.Name)
        );

        bool hasDirectColumns = canvas.Connections.Any(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "column"
        );

        bool hasDirectWildcardColumns = canvas.Connections.Any(c =>
            c.ToPin?.Owner == resultOutputNode
            && c.ToPin?.Name == "columns"
            && _isWildcardProjectionPin(c.FromPin)
        );

        if (!hasColumnListColumns && !hasDirectColumns && !hasDirectWildcardColumns)
        {
            return new QueryCompilationInputStageResult(
                Snapshot: null,
                ShouldShortCircuit: true,
                ShortCircuitSql: "-- Connect columns via Column List (columns), directly to Result Output.column, or '*' to Result Output.columns",
                Errors: errors);
        }

        (string fromTable, string? fromWarning) = _resolveFromTable(
            tableNodes,
            cteSourceNodes,
            subqueryNodes,
            cteDefinitionNamesById
        );
        if (!string.IsNullOrWhiteSpace(fromWarning))
            errors.Add(fromWarning);

        var snapshot = new QueryCompilationInputSnapshot(
            tableNodes,
            cteSourceNodes,
            subqueryNodes,
            cteDefinitions,
            cteDefinitionNamesById,
            resultOutputNode,
            fromTable,
            errors);

        return new QueryCompilationInputStageResult(
            Snapshot: snapshot,
            ShouldShortCircuit: false,
            ShortCircuitSql: null,
            Errors: errors);
    }
}



