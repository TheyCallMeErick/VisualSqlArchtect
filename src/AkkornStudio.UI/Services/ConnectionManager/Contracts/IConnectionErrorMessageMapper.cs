using AkkornStudio.Core;

namespace AkkornStudio.UI.Services.ConnectionManager;

public interface IConnectionErrorMessageMapper
{
    string Map(Exception ex, DatabaseProvider provider);
}

