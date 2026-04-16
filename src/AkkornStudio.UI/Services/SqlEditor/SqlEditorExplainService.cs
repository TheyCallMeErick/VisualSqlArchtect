using AkkornStudio.Core;
using AkkornStudio.UI.Services.ConnectionManager.Models;
using AkkornStudio.UI.Services.Explain;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlEditorExplainService
{
    private readonly IExplainExecutor _explainExecutor;

    public SqlEditorExplainService(IExplainExecutor? explainExecutor = null)
    {
        _explainExecutor = explainExecutor ?? new ExplainExecutor();
    }

    public Task<ExplainResult> RunAsync(
        string sql,
        DatabaseProvider provider,
        ConnectionConfig? connectionConfig,
        bool includeAnalyze,
        CancellationToken cancellationToken)
    {
        return _explainExecutor.RunAsync(
            sql,
            provider,
            connectionConfig,
            new ExplainOptions(IncludeAnalyze: includeAnalyze, IncludeBuffers: false, Format: ExplainFormat.Text),
            cancellationToken);
    }
}
