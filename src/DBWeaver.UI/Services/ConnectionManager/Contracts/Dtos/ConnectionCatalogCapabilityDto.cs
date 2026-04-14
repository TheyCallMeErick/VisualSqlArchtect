using DBWeaver.Core;

namespace DBWeaver.UI.Services.ConnectionManager.Contracts;

public sealed record ConnectionCatalogCapabilityDto(
    DatabaseProvider Provider,
    IReadOnlyList<ConnectionContextLevel> SupportedLevels);
