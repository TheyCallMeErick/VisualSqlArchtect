using AkkornStudio.Core;
using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.Explain;

public interface ISqlServerExplainQueryRunner
{
    Task<string> ExecuteShowPlanXmlAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    );

    Task<string> ExecuteStatisticsXmlAsync(
        string sql,
        ConnectionConfig connectionConfig,
        CancellationToken ct = default
    );
}



