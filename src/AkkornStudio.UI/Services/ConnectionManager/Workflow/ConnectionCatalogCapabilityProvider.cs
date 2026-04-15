using AkkornStudio.Core;
using AkkornStudio.UI.Services.ConnectionManager.Contracts;

namespace AkkornStudio.UI.Services.ConnectionManager;

public sealed class ConnectionCatalogCapabilityProvider : IConnectionCatalogCapabilityProvider
{
    private static readonly IReadOnlyDictionary<DatabaseProvider, ConnectionCatalogCapabilityDto> Capabilities =
        new Dictionary<DatabaseProvider, ConnectionCatalogCapabilityDto>
        {
            [DatabaseProvider.Postgres] = new(
                DatabaseProvider.Postgres,
                [ConnectionContextLevel.Connection, ConnectionContextLevel.DatabaseOrCatalog, ConnectionContextLevel.Schema]),
            [DatabaseProvider.MySql] = new(
                DatabaseProvider.MySql,
                [ConnectionContextLevel.Connection, ConnectionContextLevel.DatabaseOrCatalog, ConnectionContextLevel.Schema]),
            [DatabaseProvider.SqlServer] = new(
                DatabaseProvider.SqlServer,
                [ConnectionContextLevel.Connection, ConnectionContextLevel.DatabaseOrCatalog, ConnectionContextLevel.Schema]),
            [DatabaseProvider.SQLite] = new(
                DatabaseProvider.SQLite,
                [ConnectionContextLevel.Connection, ConnectionContextLevel.Schema]),
        };

    public IReadOnlyList<ConnectionContextLevel> GetSupportedLevels(DatabaseProvider provider)
    {
        if (Capabilities.TryGetValue(provider, out ConnectionCatalogCapabilityDto? capability))
            return capability.SupportedLevels;

        return [ConnectionContextLevel.Connection, ConnectionContextLevel.Schema];
    }
}
