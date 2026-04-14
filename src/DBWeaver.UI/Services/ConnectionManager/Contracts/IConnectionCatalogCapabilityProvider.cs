using DBWeaver.Core;

namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public interface IConnectionCatalogCapabilityProvider
{
    IReadOnlyList<ConnectionContextLevel> GetSupportedLevels(DatabaseProvider provider);
}
