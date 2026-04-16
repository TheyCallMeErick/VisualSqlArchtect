using AkkornStudio.Core;
using AkkornStudio.Metadata;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlEditorViewModelFactoryContext
{
    public DatabaseProvider InitialProvider { get; init; } = DatabaseProvider.Postgres;

    public string? InitialConnectionProfileId { get; init; }

    public required Func<ConnectionConfig?> ConnectionConfigResolver { get; init; }

    public required Func<string?, ConnectionConfig?> ConnectionConfigByProfileIdResolver { get; init; }

    public required Func<IReadOnlyList<SqlEditorConnectionProfileOption>> ConnectionProfilesResolver { get; init; }

    public required Func<DbMetadata?> MetadataResolver { get; init; }

    public required Func<ConnectionManagerViewModel?> SharedConnectionManagerResolver { get; init; }
}

