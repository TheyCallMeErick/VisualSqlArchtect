using AkkornStudio.Core;
using AkkornStudio.UI.Services.ConnectionManager;
using AkkornStudio.UI.Services.ConnectionManager.Contracts;
using Xunit;

namespace AkkornStudio.Tests.Unit.ViewModels;

public class ConnectionCatalogCapabilityProviderTests
{
    [Fact]
    public void GetSupportedLevels_ForSqlServer_IncludesDatabaseCatalogLevel()
    {
        var provider = new ConnectionCatalogCapabilityProvider();

        IReadOnlyList<ConnectionContextLevel> levels = provider.GetSupportedLevels(DatabaseProvider.SqlServer);

        Assert.Contains(ConnectionContextLevel.Connection, levels);
        Assert.Contains(ConnectionContextLevel.DatabaseOrCatalog, levels);
        Assert.Contains(ConnectionContextLevel.Schema, levels);
    }

    [Fact]
    public void GetSupportedLevels_ForSqlite_DoesNotIncludeDatabaseCatalogLevel()
    {
        var provider = new ConnectionCatalogCapabilityProvider();

        IReadOnlyList<ConnectionContextLevel> levels = provider.GetSupportedLevels(DatabaseProvider.SQLite);

        Assert.Contains(ConnectionContextLevel.Connection, levels);
        Assert.DoesNotContain(ConnectionContextLevel.DatabaseOrCatalog, levels);
        Assert.Contains(ConnectionContextLevel.Schema, levels);
    }
}
