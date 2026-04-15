using AkkornStudio.Core;

namespace AkkornStudio.UI.Services.ConnectionManager.Contracts;

public sealed record ConnectionCatalogCapabilityDto(
    DatabaseProvider Provider,
    IReadOnlyList<ConnectionContextLevel> SupportedLevels);
