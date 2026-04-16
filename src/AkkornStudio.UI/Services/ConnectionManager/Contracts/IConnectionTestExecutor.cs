using AkkornStudio.Core;

namespace AkkornStudio.UI.Services.ConnectionManager;

public interface IConnectionTestExecutor
{
    Task<ConnectionTestResult> ExecuteAsync(
        ConnectionConfig config,
        DatabaseProvider provider,
        int timeoutSeconds,
        CancellationToken ct = default);
}

