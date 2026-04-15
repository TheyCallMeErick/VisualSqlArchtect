using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Processing;

public sealed class SchemaSqlDialectEscaping
{
    public string QuoteIdentifier(DatabaseProvider provider, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return provider switch
        {
            DatabaseProvider.Postgres or DatabaseProvider.SQLite =>
                $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"",
            DatabaseProvider.SqlServer =>
                $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]",
            DatabaseProvider.MySql =>
                $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`",
            _ => throw new NotSupportedException($"Provider {provider} is not supported."),
        };
    }

    public string QuoteQualifiedName(DatabaseProvider provider, string? schemaName, string objectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        string? canonicalSchema = SchemaCanonicalizer.Normalize(provider, schemaName);
        string quotedObjectName = QuoteIdentifier(provider, objectName);

        if (string.IsNullOrWhiteSpace(canonicalSchema))
        {
            return quotedObjectName;
        }

        return $"{QuoteIdentifier(provider, canonicalSchema)}.{quotedObjectName}";
    }

    public string QuoteStringLiteral(DatabaseProvider provider, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string escaped = value.Replace("'", "''", StringComparison.Ordinal);
        return provider == DatabaseProvider.SqlServer
            ? $"N'{escaped}'"
            : $"'{escaped}'";
    }

    public string QuoteUnicodeStringLiteral(DatabaseProvider provider, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string escaped = value.Replace("'", "''", StringComparison.Ordinal);
        return provider == DatabaseProvider.SqlServer
            ? $"N'{escaped}'"
            : $"'{escaped}'";
    }
}
