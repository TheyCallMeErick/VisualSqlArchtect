using AkkornStudio.Metadata;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed record SqlCompletionMetadataIndex(
    DbMetadata Metadata,
    IReadOnlyList<SqlCompletionSuggestion> TableSuggestions,
    IReadOnlyDictionary<string, TableMetadata> TablesByFullName,
    IReadOnlyDictionary<string, TableMetadata> TablesByName,
    IReadOnlyDictionary<string, IReadOnlyList<TableMetadata>> TablesBySchema,
    IReadOnlyDictionary<string, IReadOnlyList<ColumnMetadata>> ColumnsByTable,
    IReadOnlyDictionary<string, IReadOnlyList<ForeignKeyRelation>> ForeignKeysByTable,
    IReadOnlyList<SqlCompletionSearchCandidate> SearchCandidates);
