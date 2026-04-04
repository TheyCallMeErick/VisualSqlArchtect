using VisualSqlArchitect.Core;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.Explain;

public interface IMySqlExplainQueryRunner
{
    Task<string> ExecuteFormatJsonAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    );

    Task<string> ExecuteAnalyzeAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    );
}



