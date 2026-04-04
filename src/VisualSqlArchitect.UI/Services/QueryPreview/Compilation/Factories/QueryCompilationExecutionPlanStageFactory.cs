
namespace VisualSqlArchitect.UI.Services.QueryPreview;

internal sealed class QueryCompilationExecutionPlanStageFactory(
    CanvasViewModel canvas,
    DatabaseProvider provider,
    Func<NodeViewModel, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, bool, NodeGraph> buildNodeGraph)
{
    private readonly CanvasViewModel _canvas = canvas;
    private readonly DatabaseProvider _provider = provider;
    private readonly Func<NodeViewModel, IReadOnlyList<NodeViewModel>, IReadOnlyDictionary<string, string>, bool, NodeGraph> _buildNodeGraph = buildNodeGraph;

    public QueryCompilationExecutionPlanStage Create() =>
        new(
            _buildNodeGraph,
            BuildJoins,
            ResolveSetOperation,
            ValidateCtes,
            ValidateSubqueries);

    private (List<JoinDefinition> Joins, List<string> Warnings) BuildJoins(IReadOnlyList<NodeViewModel> sourceNodes)
    {
        var joinResolver = new JoinResolver(_canvas, _provider);
        return joinResolver.BuildJoins(sourceNodes.ToList());
    }

    private (SetOperationDefinition? SetOperation, string? Warning) ResolveSetOperation(NodeViewModel resultOutputNode)
    {
        var setOpHandler = new SetOperationHandler(_canvas);
        return setOpHandler.ResolveSetOperation(resultOutputNode);
    }

    private void ValidateCtes(NodeGraph graph, IReadOnlyDictionary<string, string> cteMap, List<string> errors)
    {
        var cteValidator = new CteValidator(_canvas, cteMap, graph.Ctes);
        cteValidator.Validate(errors);
    }

    private void ValidateSubqueries(List<string> errors)
    {
        var subqueryValidator = new SubqueryValidator(_canvas);
        subqueryValidator.Validate(errors);
    }
}



