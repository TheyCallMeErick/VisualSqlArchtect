using System.Text;
using AkkornStudio.Core;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Normalization;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Processing;

public sealed class SchemaPreconditionsSqlGenerator
{
    public IReadOnlyList<string>? GenerateForeignKeyPreconditions(SchemaForeignKeyPreconditionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ChildTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ParentTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ConstraintName);
        ArgumentNullException.ThrowIfNull(request.ChildColumns);
        ArgumentNullException.ThrowIfNull(request.ParentColumns);

        if (request.ChildColumns.Count == 0 || request.ParentColumns.Count == 0)
        {
            throw new ArgumentException("ChildColumns and ParentColumns must be non-empty.", nameof(request));
        }

        if (request.ChildColumns.Count != request.ParentColumns.Count)
        {
            throw new ArgumentException("ChildColumns and ParentColumns must have the same cardinality.", nameof(request));
        }

        string schema = SchemaCanonicalizer.Normalize(request.Provider, request.SchemaName) ?? string.Empty;

        return request.Provider switch
        {
            DatabaseProvider.Postgres => BuildPostgresPreconditions(request, schema),
            DatabaseProvider.SqlServer => BuildSqlServerPreconditions(request, schema),
            DatabaseProvider.MySql => BuildMySqlPreconditions(request),
            DatabaseProvider.SQLite => null,
            _ => null,
        };
    }

    private static IReadOnlyList<string> BuildPostgresPreconditions(
        SchemaForeignKeyPreconditionRequest request,
        string schema
    )
    {
        string schemaLiteral = QuoteSqlLiteral(schema);
        string childTableLiteral = QuoteSqlLiteral(request.ChildTable);
        string parentTableLiteral = QuoteSqlLiteral(request.ParentTable);
        string constraintLiteral = QuoteSqlLiteral(request.ConstraintName);
        string childColumnsLiteral = QuoteSqlLiteral(string.Join(",", request.ChildColumns));
        string parentColumnsLiteral = QuoteSqlLiteral(string.Join(",", request.ParentColumns));

        return
        [
            $"SELECT 1 FROM information_schema.tables WHERE table_schema = '{schemaLiteral}' AND table_name = '{childTableLiteral}'",
            $"SELECT 1 FROM information_schema.tables WHERE table_schema = '{schemaLiteral}' AND table_name = '{parentTableLiteral}'",
            $"SELECT 1 FROM information_schema.table_constraints WHERE constraint_schema = '{schemaLiteral}' AND constraint_name = '{constraintLiteral}'",
            $"""
SELECT 1
FROM (
    SELECT tc.constraint_name,
           MAX(ccu.table_name) AS parent_table,
           string_agg(kcu.column_name, ',' ORDER BY kcu.ordinal_position) AS child_columns,
           string_agg(ccu.column_name, ',' ORDER BY kcu.ordinal_position) AS parent_columns
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
      ON tc.constraint_name = kcu.constraint_name
     AND tc.constraint_schema = kcu.constraint_schema
    JOIN information_schema.constraint_column_usage ccu
      ON tc.constraint_name = ccu.constraint_name
     AND tc.constraint_schema = ccu.constraint_schema
    WHERE tc.constraint_type = 'FOREIGN KEY'
      AND tc.constraint_schema = '{schemaLiteral}'
      AND tc.table_name = '{childTableLiteral}'
    GROUP BY tc.constraint_name
) fk
WHERE fk.parent_table = '{parentTableLiteral}'
  AND fk.child_columns = '{childColumnsLiteral}'
  AND fk.parent_columns = '{parentColumnsLiteral}'
"""
        ];
    }

    private static IReadOnlyList<string> BuildSqlServerPreconditions(
        SchemaForeignKeyPreconditionRequest request,
        string schema
    )
    {
        string schemaLiteral = QuoteSqlServerUnicodeLiteral(schema);
        string childTableLiteral = QuoteSqlServerUnicodeLiteral(request.ChildTable);
        string parentTableLiteral = QuoteSqlServerUnicodeLiteral(request.ParentTable);
        string constraintLiteral = QuoteSqlServerUnicodeLiteral(request.ConstraintName);
        string childColumnsLiteral = QuoteSqlServerUnicodeLiteral(string.Join(",", request.ChildColumns));
        string parentColumnsLiteral = QuoteSqlServerUnicodeLiteral(string.Join(",", request.ParentColumns));

        return
        [
            $"SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name={schemaLiteral} AND t.name={childTableLiteral}",
            $"SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name={schemaLiteral} AND t.name={parentTableLiteral}",
            $"SELECT 1 FROM sys.foreign_keys WHERE name={constraintLiteral}",
            $"""
SELECT 1
FROM (
    SELECT fk.name,
           pt.name AS parent_table,
           STRING_AGG(pc.name, ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id) AS child_columns,
           STRING_AGG(rc.name, ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id) AS parent_columns
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    JOIN sys.tables ct ON fk.parent_object_id = ct.object_id
    JOIN sys.schemas cs ON ct.schema_id = cs.schema_id
    JOIN sys.tables pt ON fk.referenced_object_id = pt.object_id
    JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
    JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
    WHERE cs.name = {schemaLiteral}
      AND ct.name = {childTableLiteral}
    GROUP BY fk.name, pt.name
) fk
WHERE fk.parent_table = {parentTableLiteral}
  AND fk.child_columns = {childColumnsLiteral}
  AND fk.parent_columns = {parentColumnsLiteral}
"""
        ];
    }

    private static IReadOnlyList<string> BuildMySqlPreconditions(SchemaForeignKeyPreconditionRequest request)
    {
        string childTableLiteral = QuoteSqlLiteral(request.ChildTable);
        string parentTableLiteral = QuoteSqlLiteral(request.ParentTable);
        string constraintLiteral = QuoteSqlLiteral(request.ConstraintName);
        string childColumnsLiteral = QuoteSqlLiteral(string.Join(",", request.ChildColumns));
        string parentColumnsLiteral = QuoteSqlLiteral(string.Join(",", request.ParentColumns));

        return
        [
            $"SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{childTableLiteral}'",
            $"SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{parentTableLiteral}'",
            $"SELECT 1 FROM information_schema.table_constraints WHERE constraint_schema = DATABASE() AND constraint_name = '{constraintLiteral}'",
            $"""
SELECT 1
FROM (
    SELECT kcu.constraint_name,
           MAX(kcu.referenced_table_name) AS parent_table,
           GROUP_CONCAT(kcu.column_name ORDER BY kcu.ordinal_position SEPARATOR ',') AS child_columns,
           GROUP_CONCAT(kcu.referenced_column_name ORDER BY kcu.ordinal_position SEPARATOR ',') AS parent_columns
    FROM information_schema.key_column_usage kcu
    WHERE kcu.table_schema = DATABASE()
      AND kcu.table_name = '{childTableLiteral}'
      AND kcu.referenced_table_name IS NOT NULL
    GROUP BY kcu.constraint_name
) fk
WHERE fk.parent_table = '{parentTableLiteral}'
  AND fk.child_columns = '{childColumnsLiteral}'
  AND fk.parent_columns = '{parentColumnsLiteral}'
"""
        ];
    }

    private static string QuoteSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string QuoteSqlServerUnicodeLiteral(string value)
    {
        return $"N'{QuoteSqlLiteral(value)}'";
    }
}
