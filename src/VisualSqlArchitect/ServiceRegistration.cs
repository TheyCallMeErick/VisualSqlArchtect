using Microsoft.Extensions.DependencyInjection;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Providers;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;

namespace VisualSqlArchitect;

// ─── Orchestrator Factory ─────────────────────────────────────────────────────

/// <summary>
/// Single creation point for <see cref="IDbOrchestrator"/> instances.
/// The canvas passes a <see cref="ConnectionConfig"/> when the user configures
/// a new data-source node; the factory resolves the correct implementation.
/// </summary>
public static class DbOrchestratorFactory
{
    private static readonly Dictionary<DatabaseProvider, Func<ConnectionConfig, IDbOrchestrator>> _factories =
        new()
        {
            [DatabaseProvider.SqlServer] = config => new SqlServerOrchestrator(config),
            [DatabaseProvider.MySql] = config => new MySqlOrchestrator(config),
            [DatabaseProvider.Postgres] = config => new PostgresOrchestrator(config),
            [DatabaseProvider.SQLite] = config => new SqliteOrchestrator(config),
        };

    public static IDbOrchestrator Create(ConnectionConfig config)
    {
        if (!_factories.TryGetValue(config.Provider, out Func<ConnectionConfig, IDbOrchestrator>? factory))
            throw new NotSupportedException($"Provider '{config.Provider}' is not supported.");

        return factory(config);
    }

    public static Func<ConnectionConfig, IDbOrchestrator>? Register(
        DatabaseProvider provider,
        Func<ConnectionConfig, IDbOrchestrator> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factories.TryGetValue(provider, out Func<ConnectionConfig, IDbOrchestrator>? previous);
        _factories[provider] = factory;
        return previous;
    }

    public static bool IsRegistered(DatabaseProvider provider) => _factories.ContainsKey(provider);
}

// ─── Active Connection Context ────────────────────────────────────────────────

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
    private readonly IProviderRegistry _providerRegistry = ProviderRegistry.CreateDefault();

    public IDbOrchestrator Orchestrator =>
        _orchestrator
        ?? throw new InvalidOperationException("No active connection. Call SwitchAsync() first.");

    public ISqlFunctionRegistry FunctionRegistry { get; private set; } =
        new SqlFunctionRegistry(DatabaseProvider.Postgres); // safe default

    public QueryBuilderService QueryBuilder { get; private set; } =
        QueryBuilderService.Create(DatabaseProvider.Postgres, "");

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
        _orchestrator = DbOrchestratorFactory.Create(config);

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

// ─── DI Extensions ───────────────────────────────────────────────────────────

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Visual SQL Architect services.
    /// Call this in Avalonia's App.axaml.cs or the composition root.
    ///
    /// <code>
    /// services.AddVisualSqlArchitect();
    /// </code>
    /// </summary>
    public static IServiceCollection AddVisualSqlArchitect(this IServiceCollection services)
    {
        // ActiveConnectionContext is a singleton so the canvas always shares
        // the same live connection across all view-models.
        services.AddSingleton<ActiveConnectionContext>();

        // FunctionRegistry and QueryBuilder are resolved from the context;
        // register factory delegates so VMs can request the current instance.
        services.AddTransient<ISqlFunctionRegistry>(sp =>
            sp.GetRequiredService<ActiveConnectionContext>().FunctionRegistry
        );

        services.AddTransient<QueryBuilderService>(sp =>
            sp.GetRequiredService<ActiveConnectionContext>().QueryBuilder
        );

        return services;
    }
}

