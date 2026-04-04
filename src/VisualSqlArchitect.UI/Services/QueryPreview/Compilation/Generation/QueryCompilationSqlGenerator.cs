
namespace VisualSqlArchitect.UI.Services.QueryPreview;

internal sealed class QueryCompilationSqlGenerator(DatabaseProvider provider) : IQueryCompilationSqlGenerator
{
    private readonly DatabaseProvider _provider = provider;

    public GeneratedQuery Generate(
        string fromTable,
        NodeGraph graph,
        IReadOnlyList<JoinDefinition> joins,
        SetOperationDefinition? setOperation)
    {
        QueryGeneratorService svc = QueryGeneratorService.Create(_provider);
        return svc.Generate(fromTable, graph, joins, setOperation);
    }
}



