using VisualSqlArchitect;
using VisualSqlArchitect.Core;

namespace VisualSqlArchitect.Tests.Unit.Core;

public class DbOrchestratorFactoryTests
{
    [Theory]
    [InlineData(DatabaseProvider.SqlServer, typeof(VisualSqlArchitect.Providers.SqlServerOrchestrator))]
    [InlineData(DatabaseProvider.MySql, typeof(VisualSqlArchitect.Providers.MySqlOrchestrator))]
    [InlineData(DatabaseProvider.Postgres, typeof(VisualSqlArchitect.Providers.PostgresOrchestrator))]
    [InlineData(DatabaseProvider.SQLite, typeof(VisualSqlArchitect.Providers.SqliteOrchestrator))]
    public void Create_ReturnsRegisteredDefaultOrchestrator(DatabaseProvider provider, Type expectedType)
    {
        var config = BuildConfig(provider);

        IDbOrchestrator orchestrator = DbOrchestratorFactory.Create(config);

        Assert.IsType(expectedType, orchestrator);
    }

    [Fact]
    public void Register_AllowsOverridingFactory_AndCanRestorePrevious()
    {
        var config = BuildConfig(DatabaseProvider.SQLite);

        Func<ConnectionConfig, IDbOrchestrator>? previous = DbOrchestratorFactory.Register(
            DatabaseProvider.SQLite,
            cfg => new FakeOrchestrator(cfg));

        try
        {
            IDbOrchestrator orchestrator = DbOrchestratorFactory.Create(config);
            Assert.IsType<FakeOrchestrator>(orchestrator);
        }
        finally
        {
            Assert.NotNull(previous);
            DbOrchestratorFactory.Register(DatabaseProvider.SQLite, previous!);
        }
    }

    private static ConnectionConfig BuildConfig(DatabaseProvider provider) =>
        new(
            Provider: provider,
            Host: "localhost",
            Port: provider == DatabaseProvider.SQLite ? 0 : 5432,
            Database: provider == DatabaseProvider.SQLite ? "test.db" : "db",
            Username: "user",
            Password: "pass");

    private sealed class FakeOrchestrator(ConnectionConfig config) : IDbOrchestrator
    {
        public DatabaseProvider Provider => config.Provider;

        public ConnectionConfig Config => config;

        public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) =>
            Task.FromResult(new ConnectionTestResult(true));

        public Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default) =>
            Task.FromResult(new DatabaseSchema("fake", config.Provider, []));

        public Task<PreviewResult> ExecutePreviewAsync(string sql, int maxRows = 200, CancellationToken ct = default) =>
            Task.FromResult(new PreviewResult(true));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
