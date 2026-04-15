using AkkornStudio.Metadata;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlCompletionMetadataIndexFactory
{
    private readonly object _sync = new();
    private SqlCompletionMetadataIndex? _cachedIndex;

    public SqlCompletionMetadataIndex GetOrCreate(DbMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        SqlCompletionMetadataIndex? cached = _cachedIndex;
        if (cached is not null && ReferenceEquals(cached.Metadata, metadata))
            return cached;

        lock (_sync)
        {
            cached = _cachedIndex;
            if (cached is not null && ReferenceEquals(cached.Metadata, metadata))
                return cached;

            SqlCompletionMetadataIndex rebuilt = Build(metadata);
            _cachedIndex = rebuilt;
            return rebuilt;
        }
    }

    private static SqlCompletionMetadataIndex Build(DbMetadata metadata)
    {
        var allTables = metadata.AllTables.ToList();
        var tableSuggestions = new List<SqlCompletionSuggestion>(Math.Max(2, allTables.Count * 2));
        var tablesByFullName = new Dictionary<string, TableMetadata>(StringComparer.OrdinalIgnoreCase);
        var tablesByName = new Dictionary<string, TableMetadata>(StringComparer.OrdinalIgnoreCase);
        var tablesBySchema = new Dictionary<string, List<TableMetadata>>(StringComparer.OrdinalIgnoreCase);
        var columnsByTable = new Dictionary<string, IReadOnlyList<ColumnMetadata>>(StringComparer.OrdinalIgnoreCase);
        var foreignKeysByTable = new Dictionary<string, IReadOnlyList<ForeignKeyRelation>>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<SqlCompletionSearchCandidate> searchCandidates = [];

        foreach (TableMetadata table in allTables)
        {
            string alias = BuildAlias(table.Name);
            string schemaName = table.Schema ?? string.Empty;

            tableSuggestions.Add(new SqlCompletionSuggestion(
                table.FullName,
                table.FullName,
                $"Table ({table.Kind})",
                SqlCompletionKind.Table));
            tableSuggestions.Add(new SqlCompletionSuggestion(
                $"{table.FullName} AS {alias}",
                $"{table.FullName} AS {alias}",
                $"Table ({table.Kind}) with alias",
                SqlCompletionKind.Table));

            tablesByFullName[table.FullName] = table;
            if (!tablesByName.ContainsKey(table.Name))
                tablesByName[table.Name] = table;

            if (!tablesBySchema.TryGetValue(schemaName, out List<TableMetadata>? schemaTables))
            {
                schemaTables = [];
                tablesBySchema[schemaName] = schemaTables;
            }

            schemaTables.Add(table);

            columnsByTable[table.FullName] = table.Columns;
            foreignKeysByTable[table.FullName] = table.OutboundForeignKeys
                .Concat(table.InboundForeignKeys)
                .ToList();

        }

        IReadOnlyDictionary<string, IReadOnlyList<TableMetadata>> finalTablesBySchema = tablesBySchema
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<TableMetadata>)pair.Value.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);

        return new SqlCompletionMetadataIndex(
            metadata,
            tableSuggestions,
            tablesByFullName,
            tablesByName,
            finalTablesBySchema,
            columnsByTable,
            foreignKeysByTable,
            searchCandidates);
    }

    private static string BuildAlias(string tableName)
    {
        string[] parts = tableName.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return "t";

        if (parts.Length == 1)
            return parts[0][0].ToString().ToLowerInvariant();

        return string.Concat(parts.Select(p => char.ToLowerInvariant(p[0])));
    }

}
