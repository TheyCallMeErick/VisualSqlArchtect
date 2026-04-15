using AkkornStudio.Core;

namespace AkkornStudio.UI.Services.SqlEditor;

public static class SqlInlineUpdateStatementBuilder
{
    public static string Build(
        DatabaseProvider provider,
        string tableFullName,
        string targetColumn,
        object? targetValue,
        IReadOnlyDictionary<string, object?> primaryKeyValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableFullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetColumn);
        ArgumentNullException.ThrowIfNull(primaryKeyValues);
        if (primaryKeyValues.Count == 0)
            throw new ArgumentException("Primary key values are required.", nameof(primaryKeyValues));

        string quotedTable = QuoteCompositeIdentifier(provider, tableFullName);
        string setClause = $"{QuoteIdentifier(provider, targetColumn)} = {ToSqlLiteral(targetValue)}";
        string whereClause = string.Join(
            " AND ",
            primaryKeyValues.Select(kvp => $"{QuoteIdentifier(provider, kvp.Key)} = {ToSqlLiteral(kvp.Value)}"));

        return $"UPDATE {quotedTable} SET {setClause} WHERE {whereClause};";
    }

    private static string QuoteCompositeIdentifier(DatabaseProvider provider, string fullName)
    {
        string[] parts = fullName.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return QuoteIdentifier(provider, fullName);

        return string.Join('.', parts.Select(part => QuoteIdentifier(provider, part)));
    }

    private static string QuoteIdentifier(DatabaseProvider provider, string identifier)
    {
        string clean = identifier.Trim().Trim('"', '`', '[', ']');
        return provider switch
        {
            DatabaseProvider.MySql => $"`{clean}`",
            DatabaseProvider.SqlServer => $"[{clean}]",
            _ => $"\"{clean}\"",
        };
    }

    private static string ToSqlLiteral(object? value)
    {
        if (value is null || value is DBNull)
            return "NULL";

        return value switch
        {
            bool b => b ? "TRUE" : "FALSE",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
                Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "0",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss.fffffff}'",
            DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss.fffffff zzz}'",
            TimeSpan ts => $"'{ts}'",
            Guid guid => $"'{guid:D}'",
            _ => $"'{value.ToString()?.Replace("'", "''", StringComparison.Ordinal) ?? string.Empty}'",
        };
    }
}
