using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;
using AkkornStudio.Metadata;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Indexing;

public sealed record SchemaMetadataIndexSnapshot(
    IReadOnlyDictionary<string, TableMetadata> TableByFullName,
    IReadOnlyDictionary<string, IReadOnlyList<ColumnMetadata>> ColumnsByTable,
    IReadOnlyDictionary<string, IReadOnlyList<ColumnMetadata>> PkColumnsByTable,
    IReadOnlyDictionary<string, IReadOnlyList<SchemaUniqueConstraintDescriptor>> UniqueConstraintsByTable,
    IReadOnlyDictionary<string, IReadOnlyList<ForeignKeyRelation>> FkByChildTable,
    IReadOnlyDictionary<string, IReadOnlyList<ForeignKeyRelation>> FkByParentTable,
    IReadOnlyDictionary<string, SchemaNormalizedNameIndexEntry> NormalizedNameIndex,
    IReadOnlyDictionary<SchemaRuleCode, RuleExecutionState> RuleExecutionState,
    IReadOnlyDictionary<string, IReadOnlySet<string>> ConstraintNamesBySchema,
    IReadOnlyDictionary<string, TableKind> TableKindsByFullName
);
