using System.Collections.Generic;

namespace AkkornStudio.UI.Services.ConnectionManager.Contracts;

public interface IConnectionTelemetryService
{
    Task TrackAsync(
        string eventName,
        IReadOnlyDictionary<string, object?> properties,
        CancellationToken cancellationToken = default);
}
