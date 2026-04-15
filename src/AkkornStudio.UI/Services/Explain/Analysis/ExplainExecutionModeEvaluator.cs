using AkkornStudio.Core;
using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.Explain;

public interface IExplainExecutionModeEvaluator
{
    bool IsSimulated(DatabaseProvider provider, ConnectionConfig? connectionConfig);
}

public sealed class ExplainExecutionModeEvaluator : IExplainExecutionModeEvaluator
{
    public bool IsSimulated(DatabaseProvider provider, ConnectionConfig? connectionConfig)
    {
        bool supportsRealExecution =
            provider == DatabaseProvider.SQLite
            || provider == DatabaseProvider.Postgres
            || provider == DatabaseProvider.MySql
            || provider == DatabaseProvider.SqlServer;

        if (!supportsRealExecution)
            return true;

        return connectionConfig is null || connectionConfig.Provider != provider;
    }
}



