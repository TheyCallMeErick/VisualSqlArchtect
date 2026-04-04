using VisualSqlArchitect.Core;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect;

/// <summary>
/// Holds the live orchestrator, function registry and query builder for the
/// currently active connection in the canvas session.
///
/// Swap <see cref="SwitchAsync"/> when the user changes data-source nodes.
/// </summary>
public sealed class ActiveConnectionContext : IAsyncDisposable
{
    private IDbOrchestrator? _orchestrator;
    private ConnectionConfig? _config;
    private readonly IProviderRegistry _providerRegistry;
    private readonly IDbOrchestratorFactory _orchestratorFactory;

    public ActiveConnectionContext(
        IProviderRegistry providerRegistry,
        IDbOrchestratorFactory orchestratorFactory
    )
    {
        _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
        _orchestratorFactory = orchestratorFactory ?? throw new ArgumentNullException(nameof(orchestratorFactory));
    }

    public IDbOrchestrator Orchestrator =>
        _orchestrator
        ?? throw new InvalidOperationException("No active connection. Call SwitchAsync() first.");

    public ISqlFunctionRegistry FunctionRegistry { get; private set; } =
        new SqlFunctionRegistry(DatabaseProvider.Postgres); // safe default

    public QueryBuilderService QueryBuilder { get; private set; } = QueryBuilderService.Create(
        DatabaseProvider.Postgres,
        ""
    );

    public DatabaseProvider Provider => _orchestrator?.Provider ?? DatabaseProvider.Postgres;

    public ConnectionConfig? Config => _config;

    /// <summary>
    /// Replaces the active connection.  Disposes the previous orchestrator
    /// gracefully before switching.
    /// </summary>
    public async Task SwitchAsync(ConnectionConfig config, CancellationToken ct = default)
    {
        if (_orchestrator is not null)
            await _orchestrator.DisposeAsync();

        _config = config;
        _orchestrator = _orchestratorFactory.Create(config);

        // Use IProviderRegistry to create components with all dependencies
        FunctionRegistry = _providerRegistry.CreateFunctionRegistry(config.Provider);
        QueryBuilder = _providerRegistry.CreateQueryBuilder(config.Provider, "");

        // Eagerly validate so the canvas shows a connection error immediately
        ConnectionTestResult test = await _orchestrator.TestConnectionAsync(ct);
        if (!test.Success)
            throw new InvalidOperationException($"Connection failed: {test.ErrorMessage}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_orchestrator is not null)
            await _orchestrator.DisposeAsync();
    }
}
