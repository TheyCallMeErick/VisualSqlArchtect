using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;
using DBWeaver.Metadata;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Indexing;

public sealed class SchemaMetadataIndexBuilder
{
    private readonly SchemaNameTokenizer _nameTokenizer = new();

    public SchemaMetadataIndexSnapshot Build(DbMetadata metadata, SchemaAnalysisProfile profile)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(profile);

        SortedDictionary<string, TableMetadata> tableByFullName = new(StringComparer.Ordinal);
        SortedDictionary<string, IReadOnlyList<ColumnMetadata>> columnsByTable = new(StringComparer.Ordinal);
        SortedDictionary<string, IReadOnlyList<ColumnMetadata>> pkColumnsByTable = new(StringComparer.Ordinal);
        SortedDictionary<string, IReadOnlyList<SchemaUniqueConstraintDescriptor>> uniqueConstraintsByTable = new(StringComparer.Ordinal);
        SortedDictionary<string, IReadOnlyList<ForeignKeyRelation>> fkByChildTable = new(StringComparer.Ordinal);
        SortedDictionary<string, IReadOnlyList<ForeignKeyRelation>> fkByParentTable = new(StringComparer.Ordinal);
        SortedDictionary<string, SchemaNormalizedNameIndexEntry> normalizedNameIndex = new(StringComparer.Ordinal);
        SortedDictionary<string, IReadOnlySet<string>> constraintNamesBySchema = new(StringComparer.Ordinal);
        SortedDictionary<string, TableKind> tableKindsByFullName = new(StringComparer.Ordinal);

        foreach (TableMetadata table in metadata.AllTables.OrderBy(static table => table.FullName, StringComparer.OrdinalIgnoreCase))
        {
            string canonicalSchema = GetSchemaKey(metadata.Provider, table.Schema);
            string tableKey = BuildTableKey(canonicalSchema, table.Name);

            tableByFullName[tableKey] = table;
            columnsByTable[tableKey] = table.Columns
                .OrderBy(static column => column.OrdinalPosition)
                .ThenBy(static column => column.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            pkColumnsByTable[tableKey] = table.Columns
                .Where(static column => column.IsPrimaryKey)
                .OrderBy(static column => column.OrdinalPosition)
                .ThenBy(static column => column.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            uniqueConstraintsByTable[tableKey] = table.Indexes
                .Where(static index => index.IsUnique && !index.IsPrimaryKey)
                .OrderBy(static index => index.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static index => new SchemaUniqueConstraintDescriptor(index.Name, index.Columns.ToList()))
                .ToList();
            tableKindsByFullName[tableKey] = table.Kind;

            AddNormalizedNameEntry(
                normalizedNameIndex,
                key: $"table|{tableKey}",
                targetType: SchemaTargetType.Table,
                schemaName: canonicalSchema,
                tableName: table.Name,
                columnName: null,
                constraintName: null,
                rawName: table.Name,
                profile: profile
            );

            foreach (ColumnMetadata column in table.Columns)
            {
                string columnKey = $"{tableKey}.{column.Name}";
                AddNormalizedNameEntry(
                    normalizedNameIndex,
                    key: $"column|{columnKey}",
                    targetType: SchemaTargetType.Column,
                    schemaName: canonicalSchema,
                    tableName: table.Name,
                    columnName: column.Name,
                    constraintName: null,
                    rawName: column.Name,
                    profile: profile
                );
            }

            foreach (IndexMetadata uniqueIndex in table.Indexes.Where(static index => index.IsUnique && !index.IsPrimaryKey))
            {
                AddConstraintName(constraintNamesBySchema, canonicalSchema, uniqueIndex.Name);
                AddNormalizedNameEntry(
                    normalizedNameIndex,
                    key: $"constraint|{canonicalSchema}|{uniqueIndex.Name}",
                    targetType: SchemaTargetType.Constraint,
                    schemaName: canonicalSchema,
                    tableName: table.Name,
                    columnName: null,
                    constraintName: uniqueIndex.Name,
                    rawName: uniqueIndex.Name,
                    profile: profile
                );
            }
        }

        foreach (IGrouping<string, ForeignKeyRelation> childGroup in metadata.AllForeignKeys
                     .OrderBy(static foreignKey => foreignKey.ConstraintName, StringComparer.OrdinalIgnoreCase)
                     .GroupBy(foreignKey =>
                         BuildTableKey(
                             GetSchemaKey(metadata.Provider, foreignKey.ChildSchema),
                             foreignKey.ChildTable
                         ),
                         StringComparer.Ordinal))
        {
            fkByChildTable[childGroup.Key] = childGroup
                .OrderBy(static foreignKey => foreignKey.ConstraintName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static foreignKey => foreignKey.OrdinalPosition)
                .ToList();
        }

        foreach (IGrouping<string, ForeignKeyRelation> parentGroup in metadata.AllForeignKeys
                     .OrderBy(static foreignKey => foreignKey.ConstraintName, StringComparer.OrdinalIgnoreCase)
                     .GroupBy(foreignKey =>
                         BuildTableKey(
                             GetSchemaKey(metadata.Provider, foreignKey.ParentSchema),
                             foreignKey.ParentTable
                         ),
                         StringComparer.Ordinal))
        {
            fkByParentTable[parentGroup.Key] = parentGroup
                .OrderBy(static foreignKey => foreignKey.ConstraintName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static foreignKey => foreignKey.OrdinalPosition)
                .ToList();
        }

        foreach (ForeignKeyRelation foreignKey in metadata.AllForeignKeys)
        {
            string canonicalSchema = GetSchemaKey(metadata.Provider, foreignKey.ChildSchema);
            AddConstraintName(constraintNamesBySchema, canonicalSchema, foreignKey.ConstraintName);
            AddNormalizedNameEntry(
                normalizedNameIndex,
                key: $"constraint|{canonicalSchema}|{foreignKey.ConstraintName}",
                targetType: SchemaTargetType.Constraint,
                schemaName: canonicalSchema,
                tableName: foreignKey.ChildTable,
                columnName: null,
                constraintName: foreignKey.ConstraintName,
                rawName: foreignKey.ConstraintName,
                profile: profile
            );
        }

        Dictionary<SchemaRuleCode, RuleExecutionState> ruleExecutionState = Enum
            .GetValues<SchemaRuleCode>()
            .ToDictionary(static ruleCode => ruleCode, static _ => RuleExecutionState.NotStarted);

        return new SchemaMetadataIndexSnapshot(
            TableByFullName: tableByFullName,
            ColumnsByTable: columnsByTable,
            PkColumnsByTable: pkColumnsByTable,
            UniqueConstraintsByTable: uniqueConstraintsByTable,
            FkByChildTable: fkByChildTable,
            FkByParentTable: fkByParentTable,
            NormalizedNameIndex: normalizedNameIndex,
            RuleExecutionState: ruleExecutionState,
            ConstraintNamesBySchema: constraintNamesBySchema,
            TableKindsByFullName: tableKindsByFullName
        );
    }

    private void AddNormalizedNameEntry(
        IDictionary<string, SchemaNormalizedNameIndexEntry> entries,
        string key,
        SchemaTargetType targetType,
        string? schemaName,
        string? tableName,
        string? columnName,
        string? constraintName,
        string rawName,
        SchemaAnalysisProfile profile
    )
    {
        entries[key] = new SchemaNormalizedNameIndexEntry(
            Key: key,
            TargetType: targetType,
            SchemaName: schemaName,
            TableName: tableName,
            ColumnName: columnName,
            ConstraintName: constraintName,
            Tokens: _nameTokenizer.Tokenize(rawName, profile)
        );
    }

    private static void AddConstraintName(
        IDictionary<string, IReadOnlySet<string>> constraintNamesBySchema,
        string schemaName,
        string constraintName
    )
    {
        if (constraintNamesBySchema.TryGetValue(schemaName, out IReadOnlySet<string>? existing))
        {
            SortedSet<string> updated = new(existing, StringComparer.OrdinalIgnoreCase)
            {
                constraintName,
            };
            constraintNamesBySchema[schemaName] = updated;
            return;
        }

        constraintNamesBySchema[schemaName] = new SortedSet<string>([constraintName], StringComparer.OrdinalIgnoreCase);
    }

    private static string GetSchemaKey(DatabaseProvider provider, string? schemaName)
    {
        return SchemaCanonicalizer.Normalize(provider, schemaName) ?? string.Empty;
    }

    private static string BuildTableKey(string schemaName, string tableName)
    {
        return string.IsNullOrWhiteSpace(schemaName) ? tableName : $"{schemaName}.{tableName}";
    }
}
