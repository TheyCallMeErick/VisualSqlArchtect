using AkkornStudio.Core;
using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.Explain;

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



